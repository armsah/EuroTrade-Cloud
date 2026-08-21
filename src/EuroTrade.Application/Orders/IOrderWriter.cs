using EuroTrade.Application.Orders.Events;
using EuroTrade.Domain.Orders;

namespace EuroTrade.Application.Orders;

public interface IOrderWriter
{
    Task AddAsync(
        Order order,
        OrderCreated orderCreated,
        CancellationToken cancellationToken = default);
}
