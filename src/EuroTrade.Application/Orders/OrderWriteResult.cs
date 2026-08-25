using EuroTrade.Domain.Orders;

namespace EuroTrade.Application.Orders;

public sealed record OrderWriteResult(
    Order Order,
    bool WasCreated);