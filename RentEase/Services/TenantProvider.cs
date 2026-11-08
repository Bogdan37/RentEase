using System.Security.Claims;

namespace RentEase.Services;

public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? CurrentTenantId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var tenantClaim = user?.FindFirst("tenantId")?.Value 
                              ?? user?.FindFirst("tenant_id")?.Value;

            if (Guid.TryParse(tenantClaim, out var tenantId))
            {
                return tenantId;
            }

            return null;
        }
    }
}