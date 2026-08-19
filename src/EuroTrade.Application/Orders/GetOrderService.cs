using EuroTrade.Domain.Orders;

namespace EuroTrade.Application.Orders;

public sealed class GetOrderService(
    IOrderRepository orderRepository)
{
    public async Task<Order?> ExecuteAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await orderRepository.GetByIdAsync(
            orderId,
            cancellationToken);
    }
}