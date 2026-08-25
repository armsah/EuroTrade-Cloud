using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;

namespace EuroTrade.Api.Authorization;

public static class OrderAuthorization
{
    public const string ReadPolicy = "Orders.Read";
    public const string WritePolicy = "Orders.Write";

    public const string ReadScope = "Orders.Read";
    public const string WriteScope = "Orders.Write";

    public static void AddOrderPolicies(
        AuthorizationOptions options)
    {
        options.AddPolicy(
            ReadPolicy,
            policy =>
            {
                policy.RequireAuthenticatedUser();

                policy.RequireAssertion(
                    context =>
                        HasScope(
                            context.User,
                            ReadScope) &&
                        HasValidTenant(
                            context.User));
            });

        options.AddPolicy(
            WritePolicy,
            policy =>
            {
                policy.RequireAuthenticatedUser();

                policy.RequireAssertion(
                    context =>
                        HasScope(
                            context.User,
                            WriteScope) &&
                        HasValidTenant(
                            context.User));
            });
    }

    private static bool HasScope(
        ClaimsPrincipal user,
        string requiredScope)
    {
        return user
            .FindAll("scp")
            .SelectMany(
                claim =>
                    claim.Value.Split(
                        ' ',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries))
            .Contains(
                requiredScope,
                StringComparer.Ordinal);
    }

    private static bool HasValidTenant(
        ClaimsPrincipal user)
    {
        var tenantIdValue =
            user.FindFirst("tenant_id")?.Value;

        return
            !string.IsNullOrWhiteSpace(
                tenantIdValue) &&
            Guid.TryParse(
                tenantIdValue,
                out var tenantId) &&
            tenantId != Guid.Empty;
    }
}