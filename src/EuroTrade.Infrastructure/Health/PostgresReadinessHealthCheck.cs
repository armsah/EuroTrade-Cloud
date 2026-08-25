using EuroTrade.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EuroTrade.Infrastructure.Health;

public sealed class PostgresReadinessHealthCheck(
    IDbContextFactory<OrdersDbContext> dbContextFactory)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext =
                await dbContextFactory.CreateDbContextAsync(
                    cancellationToken);

            var canConnect =
                await dbContext.Database.CanConnectAsync(
                    cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy(
                    "Database connection is available.")
                : HealthCheckResult.Unhealthy(
                    "Database connection is unavailable.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Database readiness check failed.",
                exception);
        }
    }
}