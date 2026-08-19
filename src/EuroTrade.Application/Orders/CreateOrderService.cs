using EuroTrade.Application.Messaging;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Domain.Orders;

namespace EuroTrade.Application.Orders;

public sealed class CreateOrderService(
    IOrderRepository orderRepository,
    IEventBus eventBus)
{
    public async Task<CreateOrderResult> ExecuteAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var order = Order.Create(
            command.TenantId,
            command.CustomerId,
            command.ProductId,
            command.Quantity);

        await orderRepository.AddAsync(
            order,
            cancellationToken);

        await eventBus.PublishAsync(
            new OrderCreated(
                order.Id,
                order.TenantId,
                order.CustomerId,
                order.ProductId,
                order.Quantity,
                order.CreatedAt),
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