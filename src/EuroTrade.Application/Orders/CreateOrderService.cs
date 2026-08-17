using EuroTrade.Domain.Orders;

namespace EuroTrade.Application.Orders;

public sealed class CreateOrderService
{
    public CreateOrderResult Execute(CreateOrderCommand command)
    {
        var order = Order.Create(
            command.TenantId,
            command.CustomerId,
            command.ProductId,
            command.Quantity);

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