using EuroTrade.Application.Tenancy;
using EuroTrade.Domain.Orders;

namespace EuroTrade.Application.Orders;

public sealed class GetOrderService(
    IOrderRepository orderRepository,
    ITenantContext tenantContext)
{
    public async Task<Order?> ExecuteAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await orderRepository.GetByTenantAndIdAsync(
            tenantContext.TenantId,
            orderId,
            cancellationToken);
    }
}