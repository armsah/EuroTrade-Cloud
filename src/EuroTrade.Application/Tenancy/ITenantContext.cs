namespace EuroTrade.Application.Tenancy;

public interface ITenantContext
{
    Guid TenantId { get; }
}