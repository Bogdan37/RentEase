using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using RentEase.Controllers;
using RentEase.Data;
using RentEase.Models;
using Xunit;

namespace RentEase.Tests;

public class PropertiesViewControllerTests
{
    private RentEaseDbContext GetDatabaseContext()
    {
        var options = new DbContextOptionsBuilder<RentEaseDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new RentEaseDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static Mock<IDistributedCache> CreateMockCache()
    {
        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.RemoveAsync(It.IsAny<string>(), default))
                 .Returns(Task.CompletedTask);
        return mockCache;
    }

    [Fact]
    public async Task Index_ReturnsViewResult_WithListOfProperties()
    {
        using var context = GetDatabaseContext();
        var mockCache = CreateMockCache();

        var property = new Property
        {
            Id = Guid.NewGuid(),
            Title = "Тестова квартира",
            City = "Київ",
            Address = "вул. Хрещатик, 1",
            PricePerMonth = 25000,
            AreaSqM = 50,
            RoomsCount = 2,
            CreatedAt = DateTime.UtcNow
        };
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var controller = new PropertiesViewController(context, mockCache.Object);

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<IEnumerable<Property>>().Subject;
        model.Should().HaveCount(1);
        model.First().Title.Should().Be("Тестова квартира");
    }

    [Fact]
    public async Task Create_ValidProperty_AddsToDatabase_AndRedirectsToIndex()
    {
        using var context = GetDatabaseContext();
        var mockCache = CreateMockCache();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Тестовий тенант",
            Slug = "test-tenant",
            CreatedAt = DateTime.UtcNow
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Богдан Мігунов",
            Email = "test@example.com",
            PhoneNumber = "+380670000000",
            CreatedAt = DateTime.UtcNow,
        };
        context.Tenants.Add(tenant);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = new PropertiesViewController(context, mockCache.Object);

        var newProperty = new Property
        {
            Title = "Нова квартира для тесту",
            City = "Львів",
            Address = "пл. Ринок, 10",
            PricePerMonth = 18000,
            AreaSqM = 45,
            RoomsCount = 1
        };
        const string testImageUrl = "https://example.com/photo.jpg";

        var result = await controller.Create(newProperty, testImageUrl);

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be(nameof(PropertiesViewController.Index));

        var savedProperty = await context.Properties.Include(p => p.Images).FirstOrDefaultAsync();
        savedProperty.Should().NotBeNull();
        savedProperty!.Title.Should().Be("Нова квартира для тесту");
        savedProperty.OwnerId.Should().Be(user.Id);
        savedProperty.Images.Should().HaveCount(1);
        savedProperty.Images.First().FileUrl.Should().Be(testImageUrl);

        mockCache.Verify(c => c.RemoveAsync("properties_all", default), Times.Once);
    }

    [Fact]
    public async Task Create_WhenModelIsInvalid_ReturnsViewResultWithSameModel()
    {
        using var context = GetDatabaseContext();
        var mockCache = CreateMockCache();
        var controller = new PropertiesViewController(context, mockCache.Object);

        controller.ModelState.AddModelError("Title", "Назва є обов'язковою");

        var invalidProperty = new Property();

        var result = await controller.Create(invalidProperty, string.Empty);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.Model.Should().Be(invalidProperty);
        (await context.Properties.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Delete_ExistingProperty_RemovesFromDatabase_AndInvalidatesCache()
    {
        using var context = GetDatabaseContext();
        var mockCache = CreateMockCache();

        var propertyId = Guid.NewGuid();
        var property = new Property
        {
            Id = propertyId,
            Title = "Квартира під видалення",
            City = "Одеса",
            Address = "вул. Дерибасівська, 5",
            PricePerMonth = 22000,
            AreaSqM = 60,
            RoomsCount = 3,
            CreatedAt = DateTime.UtcNow
        };
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var controller = new PropertiesViewController(context, mockCache.Object);

        var result = await controller.Delete(propertyId);

        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be(nameof(PropertiesViewController.Index));

        (await context.Properties.FindAsync(propertyId)).Should().BeNull();
        mockCache.Verify(c => c.RemoveAsync("properties_all", default), Times.Once);
    }

    [Fact]
    public async Task Book_ValidData_CreatesBooking_AndRedirectsToIndex()
    {
        using var context = GetDatabaseContext();
        var mockCache = CreateMockCache();

        var ownerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var property = new Property
        {
            Id = propertyId,
            TenantId = tenantId,
            OwnerId = ownerId,
            Title = "Затишна квартира",
            City = "Київ",
            Address = "Хрещатик, 1",
            PricePerMonth = 30000m,
            RoomsCount = 1
        };
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var controller = new PropertiesViewController(context, mockCache.Object);
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var endDate = startDate.AddDays(3);

        var result = await controller.Book(propertyId, clientId, startDate, endDate);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(PropertiesViewController.Index));

        var booking = await context.BookingRequests.FirstOrDefaultAsync();
        booking.Should().NotBeNull();
        booking!.PropertyId.Should().Be(propertyId);
        booking.ClientId.Should().Be(clientId);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.TotalPrice.Should().Be(3000m);
    }

    [Fact]
    public async Task Book_WhenOwnerTriesToBookOwnProperty_ReturnsError()
    {
        using var context = GetDatabaseContext();
        var mockCache = CreateMockCache();

        var ownerId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();

        var property = new Property
        {
            Id = propertyId,
            OwnerId = ownerId,
            Title = "Власна квартира",
            City = "Київ",
            Address = "вул. Франка, 10",
            PricePerMonth = 15000m,
            RoomsCount = 1
        };
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var controller = new PropertiesViewController(context, mockCache.Object);
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var endDate = startDate.AddDays(2);

        var result = await controller.Book(propertyId, ownerId, startDate, endDate);

        result.Should().BeOfType<ViewResult>();
        controller.ModelState.IsValid.Should().BeFalse();
        controller.ModelState[string.Empty]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("Власник не може забронювати"));
        (await context.BookingRequests.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Book_WhenDatesConflict_ReturnsError()
    {
        using var context = GetDatabaseContext();
        var mockCache = CreateMockCache();

        var ownerId = Guid.NewGuid();
        var client1Id = Guid.NewGuid();
        var client2Id = Guid.NewGuid();
        var propertyId = Guid.NewGuid();

        var property = new Property
        {
            Id = propertyId,
            OwnerId = ownerId,
            Title = "Студія",
            City = "Київ",
            Address = "Борщагівська, 1",
            PricePerMonth = 15000m,
            RoomsCount = 1
        };
        context.Properties.Add(property);

        var existingBooking = new BookingRequest
        {
            Id = Guid.NewGuid(),
            PropertyId = propertyId,
            ClientId = client1Id,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)),
            Status = BookingStatus.Pending,
            TotalPrice = 2500m
        };
        context.BookingRequests.Add(existingBooking);
        await context.SaveChangesAsync();

        var controller = new PropertiesViewController(context, mockCache.Object);

        var conflictStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12));
        var conflictEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(17));

        var result = await controller.Book(propertyId, client2Id, conflictStart, conflictEnd);

        result.Should().BeOfType<ViewResult>();
        controller.ModelState.IsValid.Should().BeFalse();
        controller.ModelState[string.Empty]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("вже заброньовано"));
        (await context.BookingRequests.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Book_WhenStartDateIsAfterEndDate_ReturnsError()
    {
        using var context = GetDatabaseContext();
        var mockCache = CreateMockCache();

        var propertyId = Guid.NewGuid();
        var property = new Property
        {
            Id = propertyId,
            OwnerId = Guid.NewGuid(),
            Title = "Студія",
            City = "Київ",
            Address = "вул. Польова, 2",
            PricePerMonth = 15000m,
            RoomsCount = 1
        };
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var controller = new PropertiesViewController(context, mockCache.Object);
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));

        var result = await controller.Book(propertyId, Guid.NewGuid(), startDate, endDate);

        result.Should().BeOfType<ViewResult>();
        controller.ModelState.IsValid.Should().BeFalse();
        controller.ModelState[string.Empty]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("раніше дати завершення"));
    }
}