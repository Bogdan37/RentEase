namespace RentEase.Services;

public interface ITenantProvider
{
    Guid? CurrentTenantId { get; }
}