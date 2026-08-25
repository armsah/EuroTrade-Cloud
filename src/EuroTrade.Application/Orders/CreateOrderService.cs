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
        if (string.IsNullOrWhiteSpace(
                command.IdempotencyKey))
        {
            throw new ArgumentException(
                "Idempotency-Key is required.",
                nameof(command.IdempotencyKey));
        }

        var idempotencyKey =
            command.IdempotencyKey.Trim();

        if (idempotencyKey.Length > 200)
        {
            throw new ArgumentException(
                "Idempotency-Key must not exceed 200 characters.",
                nameof(command.IdempotencyKey));
        }

        var tenantId =
            tenantContext.TenantId;

        var order =
            Order.Create(
                tenantId,
                command.CustomerId,
                command.ProductId,
                command.Quantity);

        var orderCreated =
            new OrderCreated(
                order.Id,
                order.TenantId,
                order.CustomerId,
                order.ProductId,
                order.Quantity,
                order.CreatedAt);

        var writeResult =
            await orderWriter.AddAsync(
                order,
                orderCreated,
                idempotencyKey,
                cancellationToken);

        var persistedOrder =
            writeResult.Order;

        return new CreateOrderResult(
            persistedOrder.Id,
            persistedOrder.TenantId,
            persistedOrder.CustomerId,
            persistedOrder.ProductId,
            persistedOrder.Quantity,
            persistedOrder.Status.ToString(),
            persistedOrder.CreatedAt);
    }
}