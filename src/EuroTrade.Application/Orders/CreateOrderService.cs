using EuroTrade.Application.Orders.Events;
using EuroTrade.Application.Tenancy;
using EuroTrade.Domain.Orders;

namespace EuroTrade.Application.Orders;

public sealed class CreateOrderService(
    IOrderWriter orderWriter,
    ITenantContext tenantContext)
{
    public async Task<CreateOrderResult> ExecuteAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TenantId;

        var order = Order.Create(
            tenantId,
            command.CustomerId,
            command.ProductId,
            command.Quantity);

        var orderCreated = new OrderCreated(
            order.Id,
            order.TenantId,
            order.CustomerId,
            order.ProductId,
            order.Quantity,
            order.CreatedAt);

        await orderWriter.AddAsync(
            order,
            orderCreated,
            cancellationToken);

        return new CreateOrderResult(
            order.Id,
            order.TenantId,
            order.CustomerId,
            order.ProductId,
            order.Quantity,
            order.Status.ToString(),
            order.CreatedAt);
    }
}