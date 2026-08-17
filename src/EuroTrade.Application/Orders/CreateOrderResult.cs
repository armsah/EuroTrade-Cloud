namespace EuroTrade.Application.Orders;

public sealed record CreateOrderResult(
    Guid OrderId,
    Guid TenantId,
    Guid CustomerId,
    Guid ProductId,
    int Quantity,
    string Status,
    DateTimeOffset CreatedAt);