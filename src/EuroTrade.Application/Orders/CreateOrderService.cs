using System.Diagnostics;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Application.Telemetry;
using EuroTrade.Domain.Orders;

namespace EuroTrade.Application.Orders;

public sealed class CreateOrderService(
    IOrderWriter orderWriter)
{
    public async Task<CreateOrderResult> ExecuteAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        using var activity = EuroTradeActivitySource.Source.StartActivity(
            "CreateOrder",
            ActivityKind.Internal);

        activity?.SetTag("order.tenant_id", command.TenantId);
        activity?.SetTag("order.customer_id", command.CustomerId);
        activity?.SetTag("order.product_id", command.ProductId);
        activity?.SetTag("order.quantity", command.Quantity);

        var order = Order.Create(
            command.TenantId,
            command.CustomerId,
            command.ProductId,
            command.Quantity);

        activity?.SetTag("order.id", order.Id);
        activity?.SetTag("order.status", order.Status.ToString());

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

        activity?.SetStatus(ActivityStatusCode.Ok);

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