using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;

using EuroTrade.Api.Authorization;
using EuroTrade.Api.Tenancy;
using EuroTrade.Application.Orders;
using EuroTrade.Application.Tenancy;
using EuroTrade.Infrastructure;
using EuroTrade.Infrastructure.Health;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Identity.Web;

using EuroTrade.Infrastructure.Observability;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

AppContext.SetSwitch(
    "Azure.Experimental.EnableActivitySource",
    true);

var builder =
    WebApplication.CreateBuilder(args);

// ============================================================
// Azure Key Vault
// ============================================================

var keyVaultName =
    builder.Configuration["KeyVault:Name"];

if (!string.IsNullOrWhiteSpace(
        keyVaultName))
{
    var keyVaultUri =
        new Uri(
            $"https://{keyVaultName}.vault.azure.net/");

    builder.Configuration.AddAzureKeyVault(
        keyVaultUri,
        new DefaultAzureCredential());
}

// ============================================================
// Authentication / Authorization
// ============================================================

var azureAdSection =
    builder.Configuration.GetSection("AzureAd");

var azureAdInstance =
    azureAdSection["Instance"];

var azureAdTenantId =
    azureAdSection["TenantId"];

var azureAdClientId =
    azureAdSection["ClientId"];

var isProduction =
    builder.Environment.IsProduction();

if (isProduction)
{
    var missingSettings =
        new List<string>();

    if (string.IsNullOrWhiteSpace(
            azureAdInstance))
    {
        missingSettings.Add(
            "AzureAd:Instance");
    }

    if (string.IsNullOrWhiteSpace(
            azureAdTenantId))
    {
        missingSettings.Add(
            "AzureAd:TenantId");
    }

    if (string.IsNullOrWhiteSpace(
            azureAdClientId))
    {
        missingSettings.Add(
            "AzureAd:ClientId");
    }

    if (missingSettings.Count > 0)
    {
        throw new InvalidOperationException(
            "Azure AD authentication configuration is incomplete. " +
            "The following settings are required in Production: " +
            string.Join(
                ", ",
                missingSettings) +
            ".");
    }
}

var hasAzureAdConfiguration =
    !string.IsNullOrWhiteSpace(
        azureAdInstance) &&
    !string.IsNullOrWhiteSpace(
        azureAdTenantId) &&
    !string.IsNullOrWhiteSpace(
        azureAdClientId);

if (hasAzureAdConfiguration)
{
    builder.Services
        .AddAuthentication(
            JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(
            azureAdSection,
            subscribeToJwtBearerMiddlewareDiagnosticsEvents:
                true);
}

builder.Services.AddAuthorization(
    OrderAuthorization.AddOrderPolicies);

// ============================================================
// Application / Infrastructure
// ============================================================

builder.Services.AddInfrastructure(
    builder.Configuration);

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<
    ITenantContext,
    HttpTenantContext>();

builder.Services.AddScoped<
    CreateOrderService>();

builder.Services.AddScoped<
    GetOrderService>();

// ============================================================
// Health checks
// ============================================================

builder.Services
    .AddHealthChecks()

    // Liveness deliberately checks only whether the
    // ASP.NET process is alive.
    .AddCheck(
        "self",
        () =>
            HealthCheckResult.Healthy(
                "Application process is alive."),
        tags:
            ["live"])

    // Readiness verifies that this API replica can reach
    // the Orders database.
    .AddCheck<PostgresReadinessHealthCheck>(
        "orders-database",
        failureStatus:
            HealthStatus.Unhealthy,
        tags:
            ["ready"]);

// ============================================================
// Application Insights / Azure Monitor
// ============================================================

var applicationInsightsConnectionString =
    builder.Configuration[
        "APPLICATIONINSIGHTS_CONNECTION_STRING"]
    ?? builder.Configuration[
        "ApplicationInsights:ConnectionString"];

var serviceVersion =
    typeof(Program)
        .Assembly
        .GetName()
        .Version?
        .ToString()
    ?? "unknown";

var deploymentEnvironment =
    builder.Environment.EnvironmentName;

var openTelemetry =
    builder.Services
        .AddOpenTelemetry()
        .ConfigureResource(
            resource =>
                resource
                    .AddService(
                        serviceName: "EuroTrade.Api",
                        serviceVersion: serviceVersion)
                    .AddAttributes(
                    [
                        new KeyValuePair<string, object>(
                            "deployment.environment.name",
                            deploymentEnvironment)
                    ]))
        .WithTracing(
            tracing =>
            {
                tracing
                    .AddSource("EuroTrade.Application")
                    .AddSource("Azure.*")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            })
        .WithMetrics(
            metrics =>
            {
                metrics
                    .AddMeter(
                        EuroTradeMetrics.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            });

if (!string.IsNullOrWhiteSpace(
        applicationInsightsConnectionString))
{
    openTelemetry.UseAzureMonitorExporter(
        options =>
        {
            options.ConnectionString =
                applicationInsightsConnectionString;
        });
}

// ============================================================
// Build application
// ============================================================

var app =
    builder.Build();

// ============================================================
// Authentication / Authorization middleware
// ============================================================

app.UseAuthentication();
app.UseAuthorization();

// ============================================================
// Health endpoints
// ============================================================

// Liveness:
// "Is this process alive?"
//
// No database, Service Bus, Key Vault or other external
// dependency is included here.
app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate =
            registration =>
                registration.Tags.Contains(
                    "live")
    });

// Readiness:
// "Can this replica currently serve application traffic?"
//
// PostgreSQL/OrdersDb is part of readiness.
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate =
            registration =>
                registration.Tags.Contains(
                    "ready")
    });

// ============================================================
// Create order
// ============================================================

app.MapPost(
    "/api/orders",
    async (
        CreateOrderRequest request,
        HttpRequest httpRequest,
        CreateOrderService service,
        CancellationToken cancellationToken) =>
    {
        if (!httpRequest.Headers.TryGetValue(
                "Idempotency-Key",
                out var idempotencyKeyValues) ||
            idempotencyKeyValues.Count != 1 ||
            string.IsNullOrWhiteSpace(
                idempotencyKeyValues[0]))
        {
            return Results.BadRequest(
                new
                {
                    error =
                        "Idempotency-Key header is required."
                });
        }

        var command =
            new CreateOrderCommand(
                request.CustomerId,
                request.ProductId,
                request.Quantity,
                idempotencyKeyValues[0]!);

        try
        {
            var result =
                await service.ExecuteAsync(
                    command,
                    cancellationToken);

            return Results.Created(
                $"/api/orders/{result.OrderId}",
                result);
        }
        catch (
            IdempotencyConflictException exception)
        {
            return Results.Conflict(
                new
                {
                    error =
                        exception.Message
                });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(
                new
                {
                    error =
                        exception.Message
                });
        }
    })
    .RequireAuthorization(
        OrderAuthorization.WritePolicy);

// ============================================================
// Get order
// ============================================================

app.MapGet(
    "/api/orders/{orderId:guid}",
    async (
        Guid orderId,
        GetOrderService service,
        CancellationToken cancellationToken) =>
    {
        var order =
            await service.ExecuteAsync(
                orderId,
                cancellationToken);

        if (order is null)
        {
            return Results.NotFound(
                new
                {
                    error =
                        "Order not found."
                });
        }

        return Results.Ok(
            new
            {
                orderId =
                    order.Id,

                tenantId =
                    order.TenantId,

                customerId =
                    order.CustomerId,

                productId =
                    order.ProductId,

                quantity =
                    order.Quantity,

                status =
                    order.Status.ToString(),

                createdAt =
                    order.CreatedAt
            });
    })
    .RequireAuthorization(
        OrderAuthorization.ReadPolicy);

// ============================================================
// Run application
// ============================================================

app.Run();

public partial class Program;

public sealed record CreateOrderRequest(
    Guid CustomerId,
    Guid ProductId,
    int Quantity);