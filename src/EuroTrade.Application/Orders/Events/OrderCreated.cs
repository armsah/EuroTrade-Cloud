namespace EuroTrade.Application.Orders.Events;

public sealed record OrderCreated(
    Guid OrderId,
    Guid TenantId,
    Guid CustomerId,
    Guid ProductId,
    int Quantity,
    DateTimeOffset CreatedAt);