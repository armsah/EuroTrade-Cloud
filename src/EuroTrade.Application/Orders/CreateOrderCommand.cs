namespace EuroTrade.Application.Orders;

public sealed record CreateOrderCommand(
    Guid CustomerId,
    Guid ProductId,
    int Quantity);