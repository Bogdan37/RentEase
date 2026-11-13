using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.Mvc;
using RentEase.Data;
using RentEase.Models;

namespace RentEase.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IFeatureManager _featureManager;
    private readonly RentEaseDbContext _context;

    public AnalyticsController(IFeatureManager featureManager, RentEaseDbContext context)
    {
        _featureManager = featureManager;
        _context = context;
    }

    [HttpGet("premium-report")]
    [FeatureGate("PremiumAnalytics")]
    public async Task<IActionResult> GetPremiumReport()
    {
        var userIdClaim =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        Guid.TryParse(userIdClaim, out var ownerId);

        var allProperties = await _context.Properties
            .IgnoreQueryFilters()
            .ToListAsync();

        var allBookings = await _context.BookingRequests
            .IgnoreQueryFilters()
            .Include(b => b.Property)
            .ToListAsync();

        var ownerProperties = ownerId != Guid.Empty
            ? allProperties.Where(p => p.OwnerId == ownerId).ToList()
            : allProperties;

        var ownerPropIds = ownerProperties.Select(p => p.Id).ToHashSet();

        var ownerBookings = ownerId != Guid.Empty
            ? allBookings.Where(b => ownerPropIds.Contains(b.PropertyId)).ToList()
            : allBookings;

        var activeOwnerBookings = ownerBookings
            .Where(b => b.Status != BookingStatus.Cancelled)
            .ToList();

        var confirmedRevenue = ownerBookings
            .Where(b => b.Status == BookingStatus.Confirmed)
            .Sum(b => b.TotalPrice);

        var pendingRevenue = ownerBookings
            .Where(b => b.Status == BookingStatus.Pending)
            .Sum(b => b.TotalPrice);

        var projectedMonthlyRevenue = ownerProperties.Sum(p => p.PricePerMonth);

        // Розрахунок завантаженості на найближчі 30 днів
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizonEnd = today.AddDays(30);
        var totalAvailableDays = Math.Max(ownerProperties.Count * 30, 1);

        var bookedDaysInNext30 = 0;
        foreach (var b in activeOwnerBookings)
        {
            var overlapStart = b.StartDate > today ? b.StartDate : today;
            var overlapEnd = b.EndDate < horizonEnd ? b.EndDate : horizonEnd;
            if (overlapEnd > overlapStart)
            {
                bookedDaysInNext30 += overlapEnd.DayNumber - overlapStart.DayNumber;
            }
        }

        var occupancyRate = ownerProperties.Count == 0
            ? 0
            : Math.Round(Math.Min(100.0, (double)bookedDaysInNext30 / totalAvailableDays * 100.0), 1);

        var averageStayDays = activeOwnerBookings.Count == 0
            ? 0
            : Math.Round(activeOwnerBookings.Average(b => Math.Max(1, b.EndDate.DayNumber - b.StartDate.DayNumber)), 1);

        var ownerAvgDailyPrice = ownerProperties.Count == 0
            ? 0
            : Math.Round(ownerProperties.Average(p => p.PricePerMonth / 30m), 0);

        var marketAvgDailyPrice = allProperties.Count == 0
            ? 0
            : Math.Round(allProperties.Average(p => p.PricePerMonth / 30m), 0);

        var topProperty = ownerProperties
            .Select(p => new
            {
                p.Title,
                Revenue = activeOwnerBookings
                    .Where(b => b.PropertyId == p.Id)
                    .Sum(b => b.TotalPrice)
            })
            .OrderByDescending(x => x.Revenue)
            .FirstOrDefault();

        var topLocations = allBookings
            .Where(b => b.Status != BookingStatus.Cancelled && b.Property != null)
            .GroupBy(b => $"{b.Property!.City}, {b.Property.Address}")
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => new
            {
                Location = g.Key,
                BookingsCount = g.Count()
            })
            .ToList();

        return Ok(new
        {
            Feature = "PremiumAnalytics",
            Status = "Active",
            TotalProperties = ownerProperties.Count,
            ActiveBookingsCount = activeOwnerBookings.Count,
            OccupancyRatePercent = occupancyRate,
            ConfirmedRevenue = confirmedRevenue,
            PendingRevenue = pendingRevenue,
            ProjectedMonthlyRevenue = projectedMonthlyRevenue,
            AverageStayDays = averageStayDays,
            OwnerAvgDailyPrice = ownerAvgDailyPrice,
            MarketAvgDailyPrice = marketAvgDailyPrice,
            TopPropertyTitle = topProperty?.Title ?? "Ще немає даних",
            TopPropertyRevenue = topProperty?.Revenue ?? 0m,
            TopLocations = topLocations,
            Recommendation = occupancyRate >= 75
                ? "Високий попит! Ви можете підвищити добову ставку на 10–15% без втрати завантаженості."
                : ownerProperties.Count == 0
                    ? "Додайте перший об'єкт нерухомості, щоб система сформувала прогноз дохідності."
                    : "Попит помірний: активуйте промо-знижку або додайте якісні фото для зростання бронювань."
        });
    }

    [HttpGet("pricing-plan")]
    public async Task<IActionResult> GetPricingPlan()
    {
        var hasDiscount = await _featureManager.IsEnabledAsync("InstantBookingDiscount");

        return Ok(new
        {
            PlanName = "Standard",
            BaseFeePercent = 10,
            DiscountApplied = hasDiscount,
            EffectiveFeePercent = hasDiscount ? 7 : 10,
            Notice = hasDiscount
                ? "Активно промо: знижка 3% на комісію сервісу!"
                : "Знижки наразі відсутні."
        });
    }
}