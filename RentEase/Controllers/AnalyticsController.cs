using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.Mvc;

namespace RentEase.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IFeatureManager _featureManager;

    public AnalyticsController(IFeatureManager featureManager)
    {
        _featureManager = featureManager;
    }


    [HttpGet("premium-report")]
    [FeatureGate("PremiumAnalytics")]
    public IActionResult GetPremiumReport()
    {
        return Ok(new
        {
            Feature = "PremiumAnalytics",
            Status = "Active",
            MonthlyOccupancyRate = "92.4%",
            ProjectedRevenue = 158000.00,
            TopLocations = new[] { "Київ, Печерськ", "Київ, Поділ" },
            Message = "Це ексклюзивний звіт для користувачів преміум-тарифу."
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