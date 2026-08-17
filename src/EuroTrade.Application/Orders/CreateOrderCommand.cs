namespace EuroTrade.Application.Orders;

public sealed record CreateOrderCommand(
    Guid TenantId,
    Guid CustomerId,
    Guid ProductId,
    int Quantity);