using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using EuroTrade.Application.Orders;
using EuroTrade.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using OpenTelemetry.Trace;

AppContext.SetSwitch(
    "Azure.Experimental.EnableActivitySource",
    true);

var builder = WebApplication.CreateBuilder(args);

var keyVaultName =
    builder.Configuration["KeyVault:Name"];

if (!string.IsNullOrWhiteSpace(keyVaultName))
{
    var keyVaultUri = new Uri(
        $"https://{keyVaultName}.vault.azure.net/");

    builder.Configuration.AddAzureKeyVault(
        keyVaultUri,
        new DefaultAzureCredential());
}

var azureAdClientId =
    builder.Configuration["AzureAd:ClientId"];

if (!string.IsNullOrWhiteSpace(azureAdClientId))
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(
            builder.Configuration,
            "AzureAd");
}

builder.Services.AddAuthorization();

builder.Services.AddInfrastructure(
    builder.Configuration);

builder.Services.AddScoped<CreateOrderService>();
builder.Services.AddScoped<GetOrderService>();

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

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(azureAdClientId))
{
    app.UseAuthentication();
}

app.UseAuthorization();

app.MapGet(
    "/health",
    () =>
        Results.Ok(
            new
            {
                status = "healthy"
            }));

app.MapPost(
    "/api/orders",
    async (
        CreateOrderRequest request,
        CreateOrderService service,
        CancellationToken cancellationToken) =>
    {
        var command = new CreateOrderCommand(
            request.TenantId,
            request.CustomerId,
            request.ProductId,
            request.Quantity);

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
        catch (ArgumentException exception)
        {
            return Results.BadRequest(
                new
                {
                    error = exception.Message
                });
        }
    })
    .RequireAuthorization();

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
                    error = "Order not found."
                });
        }

        return Results.Ok(
            new
            {
                orderId = order.Id,
                tenantId = order.TenantId,
                customerId = order.CustomerId,
                productId = order.ProductId,
                quantity = order.Quantity,
                status = order.Status.ToString(),
                createdAt = order.CreatedAt
            });
    })
    .RequireAuthorization();

app.Run();

public partial class Program;

public sealed record CreateOrderRequest(
    Guid TenantId,
    Guid CustomerId,
    Guid ProductId,
    int Quantity);
