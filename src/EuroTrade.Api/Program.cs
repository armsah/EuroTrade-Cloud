using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;

using EuroTrade.Api.Tenancy;
using EuroTrade.Application.Orders;
using EuroTrade.Application.Tenancy;
using EuroTrade.Infrastructure;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

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

var azureAdClientId =
    builder.Configuration["AzureAd:ClientId"];

if (!string.IsNullOrWhiteSpace(
        azureAdClientId))
{
    builder.Services
        .AddAuthentication(
            JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(
            builder.Configuration
                .GetSection("AzureAd"),
            subscribeToJwtBearerMiddlewareDiagnosticsEvents:
                true);
}

builder.Services.AddAuthorization();

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
// Application Insights / Azure Monitor
// ============================================================

var applicationInsightsConnectionString =
    builder.Configuration[
        "APPLICATIONINSIGHTS_CONNECTION_STRING"]
    ?? builder.Configuration[
        "ApplicationInsights:ConnectionString"];

var openTelemetry =
    builder.Services
        .AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing
                .AddSource("EuroTrade.Application")
                .AddSource("Azure.*")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();
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
// Health endpoint
// ============================================================

app.MapGet(
    "/health",
    () =>
        Results.Ok(
            new
            {
                status = "healthy"
            }));

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
    .RequireAuthorization();

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
    .RequireAuthorization();

// ============================================================
// Run application
// ============================================================

app.Run();

public partial class Program;

public sealed record CreateOrderRequest(
    Guid CustomerId,
    Guid ProductId,
    int Quantity);