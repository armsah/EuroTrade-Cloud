using EuroTrade.Application.Orders.Events;
using EuroTrade.Domain.Orders;

namespace EuroTrade.Application.Orders;

public interface IOrderWriter
{
    Task<OrderWriteResult> AddAsync(
        Order order,
        OrderCreated orderCreated,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}