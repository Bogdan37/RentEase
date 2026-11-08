using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using RentEase.Controllers;
using RentEase.Data;
using RentEase.DTOs;
using RentEase.Models;
using RentEase.Services;
using Xunit;

namespace RentEase.Tests;

public class PropertiesControllerTests
{
    private (RentEaseDbContext Context, Mock<ITenantProvider> MockTenant) GetDatabaseContext(Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<RentEaseDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mockTenant = new Mock<ITenantProvider>();
        mockTenant.Setup(t => t.CurrentTenantId).Returns(tenantId);

        var context = new RentEaseDbContext(options, mockTenant.Object);
        context.Database.EnsureCreated();
        return (context, mockTenant);
    }

    [Fact]
    public async Task GetAll_Pagination_ReturnsCorrectSliceAndMetadata()
    {
        var (context, _) = GetDatabaseContext();
        using (context)
        {
            var mockCache = new Mock<IDistributedCache>();
            var mockSearch = new Mock<IPropertySearchService>();
            var mockLogger = new Mock<ILogger<PropertiesController>>();

            for (int i = 1; i <= 5; i++)
            {
                context.Properties.Add(new Property
                {
                    Id = Guid.NewGuid(),
                    Title = $"Квартира {i}",
                    City = "Київ",
                    Address = $"Вулиця {i}",
                    PricePerMonth = 10000 + i * 500,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await context.SaveChangesAsync();

            var controller = new PropertiesController(
                context, 
                mockCache.Object, 
                mockSearch.Object, 
                mockLogger.Object);

            var result = await controller.GetAll(page: 2, pageSize: 2, CancellationToken.None);

            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var pagedResult = okResult.Value.Should().BeOfType<PagedResult<PropertyResponseDto>>().Subject;

            pagedResult.Items.Should().HaveCount(2);
            pagedResult.Page.Should().Be(2);
            pagedResult.PageSize.Should().Be(2);
            pagedResult.TotalCount.Should().Be(5);
            pagedResult.TotalPages.Should().Be(3);
            pagedResult.HasPreviousPage.Should().BeTrue();
            pagedResult.HasNextPage.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Search_DelegatesToLucene_AndReturnsProperties()
    {
        var (context, _) = GetDatabaseContext();
        using (context)
        {
            var mockCache = new Mock<IDistributedCache>();
            var mockSearch = new Mock<IPropertySearchService>();
            var mockLogger = new Mock<ILogger<PropertiesController>>();

            var prop1Id = Guid.NewGuid();
            var prop2Id = Guid.NewGuid();

            context.Properties.AddRange(
                new Property { Id = prop1Id, Title = "Квартира біля парку", City = "Київ", Address = "вул. Володимирська, 10" },
                new Property { Id = prop2Id, Title = "Студія в центрі", City = "Львів", Address = "вул. Городоцька, 20" }
            );
            await context.SaveChangesAsync();

            mockSearch.Setup(s => s.Search("парку", 20))
                      .Returns(new List<Guid> { prop1Id });

            var controller = new PropertiesController(
                context, 
                mockCache.Object, 
                mockSearch.Object, 
                mockLogger.Object);

            var result = await controller.Search("парку", CancellationToken.None);

            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var list = okResult.Value.Should().BeAssignableTo<IEnumerable<PropertyResponseDto>>().Subject.ToList();

            list.Should().HaveCount(1);
            list.First().Id.Should().Be(prop1Id);
            mockSearch.Verify(s => s.Search("парку", 20), Times.Once);
        }
    }
}