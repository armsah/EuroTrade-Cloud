using EuroTrade.Domain.Orders;

namespace EuroTrade.Application.Orders;

public interface IOrderRepository
{
    Task AddAsync(
        Order order,
        CancellationToken cancellationToken = default);

    Task<Order?> GetByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}