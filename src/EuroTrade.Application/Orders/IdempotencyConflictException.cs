namespace EuroTrade.Application.Orders;

public sealed class IdempotencyConflictException(
    string message)
    : Exception(message);