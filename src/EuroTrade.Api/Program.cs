using EuroTrade.Application.Orders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<CreateOrderService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy"
}));

app.MapPost("/api/orders", (
    CreateOrderRequest request,
    CreateOrderService service) =>
{
    var command = new CreateOrderCommand(
        request.TenantId,
        request.CustomerId,
        request.ProductId,
        request.Quantity);

    try
    {
        var result = service.Execute(command);

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

app.Run();

public sealed record CreateOrderRequest(
    Guid TenantId,
    Guid CustomerId,
    Guid ProductId,
    int Quantity);