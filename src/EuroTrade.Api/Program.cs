using EuroTrade.Application.Orders;
using EuroTrade.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<CreateOrderService>();
builder.Services.AddScoped<GetOrderService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy"
}));

app.MapPost("/api/orders", async (
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
        var result = await service.ExecuteAsync(
            command,
            cancellationToken);

        return Results.Created(
            $"/api/orders/{result.OrderId}",
            result);
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new
        {
            error = exception.Message
        });
    }
});

app.MapGet("/api/orders/{orderId:guid}", async (
    Guid orderId,
    GetOrderService service,
    CancellationToken cancellationToken) =>
{
    var order = await service.ExecuteAsync(
        orderId,
        cancellationToken);

    if (order is null)
    {
        return Results.NotFound(new
        {
            error = "Order not found."
        });
    }

    return Results.Ok(new
    {
        orderId = order.Id,
        tenantId = order.TenantId,
        customerId = order.CustomerId,
        productId = order.ProductId,
        quantity = order.Quantity,
        status = order.Status.ToString(),
        createdAt = order.CreatedAt
    });
});

app.Run();

public partial class Program;

public sealed record CreateOrderRequest(
    Guid TenantId,
    Guid CustomerId,
    Guid ProductId,
    int Quantity);