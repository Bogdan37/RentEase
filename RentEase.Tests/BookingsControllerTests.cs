using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using RentEase.Controllers;
using RentEase.Data;
using RentEase.DTOs;
using RentEase.Hubs;
using RentEase.Models;
using RentEase.Services;
using Xunit;

namespace RentEase.Tests;

public class BookingsControllerTests
{
    [Fact]
    public async Task Create_ValidBooking_SavesToDb_AndPublishesMessageToRabbitMq()
    {
        var options = new DbContextOptionsBuilder<RentEaseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var mockTenantProvider = new Mock<ITenantProvider>();
        using var context = new RentEaseDbContext(options, mockTenantProvider.Object);

        var mockProducer = new Mock<IMessageProducer>();

        var mockHubClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockHubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockHubClients.Setup(c => c.Groups(It.IsAny<IReadOnlyList<string>>())).Returns(mockClientProxy.Object);

        var mockHubContext = new Mock<IHubContext<BookingHub>>();
        mockHubContext.Setup(h => h.Clients).Returns(mockHubClients.Object);

        var ownerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();

        context.Users.AddRange(
            new User { Id = ownerId, FullName = "Власник", Email = "owner@test.com", PhoneNumber = "111", TenantId = tenantId },
            new User { Id = clientId, FullName = "Орендар", Email = "client@test.com", PhoneNumber = "222", TenantId = tenantId }
        );

        context.Properties.Add(new Property
        {
            Id = propertyId,
            TenantId = tenantId,
            OwnerId = ownerId,
            Title = "Апартаменти",
            City = "Київ",
            Address = "Хрещатик, 24",
            PricePerMonth = 30000m
        });
        await context.SaveChangesAsync();

        var controller = new BookingsController(context, mockProducer.Object, mockHubContext.Object);

        var dto = new CreateBookingDto(
            propertyId,
            clientId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(6))
        );

        var result = await controller.Create(dto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var responseDto = createdResult.Value.Should().BeOfType<BookingResponseDto>().Subject;

        responseDto.PropertyId.Should().Be(propertyId);
        responseDto.ClientId.Should().Be(clientId);

        var savedBooking = await context.BookingRequests.FirstOrDefaultAsync();
        savedBooking.Should().NotBeNull();
        savedBooking!.Status.Should().Be(BookingStatus.Pending);

        mockProducer.Verify(p => p.SendMessage(
            It.Is<BookingCreatedEvent>(e => e.BookingId == savedBooking.Id && e.PropertyId == propertyId),
            "booking_created_queue"
        ), Times.Once);

        mockHubClients.Verify(
            c => c.Group($"user_{ownerId.ToString().ToLowerInvariant()}"),
            Times.Once
        );
    }
}