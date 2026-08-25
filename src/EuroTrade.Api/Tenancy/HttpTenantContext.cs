using EuroTrade.Application.Tenancy;

namespace EuroTrade.Api.Tenancy;

public sealed class HttpTenantContext(
    IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid TenantId
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext
                ?? throw new UnauthorizedAccessException(
                    "No HTTP context is available.");

            var user = httpContext.User;

            if (user.Identity?.IsAuthenticated != true)
            {
                throw new UnauthorizedAccessException(
                    "The current user is not authenticated.");
            }

            var tenantIdValue = user.FindFirst("tenant_id")?.Value;

            if (string.IsNullOrWhiteSpace(tenantIdValue))
            {
                throw new UnauthorizedAccessException(
                    "The authenticated identity does not contain a tenant_id claim.");
            }

            if (!Guid.TryParse(tenantIdValue, out var tenantId))
            {
                throw new UnauthorizedAccessException(
                    "The authenticated identity contains an invalid tenant_id claim.");
            }

            return tenantId;
        }
    }
}