using System.Diagnostics;
using System.Text.Json;

using EuroTrade.Application.Orders;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Domain.Orders;
using EuroTrade.Infrastructure.Persistence.Idempotency;
using EuroTrade.Infrastructure.Persistence.Outbox;
using EuroTrade.Infrastructure.Observability;

using Microsoft.EntityFrameworkCore;

namespace EuroTrade.Infrastructure.Persistence;

public sealed class EfOrderWriter(
    OrdersDbContext dbContext)
    : IOrderWriter
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<OrderWriteResult> AddAsync(
        Order order,
        OrderCreated orderCreated,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var existing =
            await dbContext.IdempotencyRecords
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    record =>
                        record.TenantId ==
                            order.TenantId &&
                        record.IdempotencyKey ==
                            idempotencyKey,
                    cancellationToken);

        if (existing is not null)
        {
            return await ResolveExistingAsync(
                existing,
                order,
                cancellationToken);
        }

        var currentActivity =
            Activity.Current;

        var outboxMessage =
            new OutboxMessage
            {
                Id =
                    Guid.NewGuid(),

                MessageType =
                    nameof(OrderCreated),

                Payload =
                    JsonSerializer.Serialize(
                        orderCreated,
                        JsonOptions),

                CreatedAt =
                    DateTimeOffset.UtcNow,

                TraceParent =
                    currentActivity?.Id,

                TraceState =
                    currentActivity?.TraceStateString
            };

        var idempotencyRecord =
            new IdempotencyRecord
            {
                Id =
                    Guid.NewGuid(),

                TenantId =
                    order.TenantId,

                IdempotencyKey =
                    idempotencyKey,

                OrderId =
                    order.Id,

                CustomerId =
                    order.CustomerId,

                ProductId =
                    order.ProductId,

                Quantity =
                    order.Quantity,

                CreatedAt =
                    order.CreatedAt
            };

        await using var transaction =
            await dbContext.Database
                .BeginTransactionAsync(
                    cancellationToken);

        try
        {
            dbContext.Orders.Add(
                order);

            dbContext.OutboxMessages.Add(
                outboxMessage);

            dbContext.IdempotencyRecords.Add(
                idempotencyRecord);

            await dbContext.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            EuroTradeMetrics.OrdersCreated.Add(
                1);

            return new OrderWriteResult(
                order,
                true);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(
                cancellationToken);

            dbContext.ChangeTracker.Clear();

            var winner =
                await dbContext.IdempotencyRecords
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        record =>
                            record.TenantId ==
                                order.TenantId &&
                            record.IdempotencyKey ==
                                idempotencyKey,
                        cancellationToken);

            if (winner is null)
            {
                throw;
            }

            return await ResolveExistingAsync(
                winner,
                order,
                cancellationToken);
        }
    }

    private async Task<OrderWriteResult> ResolveExistingAsync(
        IdempotencyRecord record,
        Order requestedOrder,
        CancellationToken cancellationToken)
    {
        if (record.CustomerId !=
                requestedOrder.CustomerId ||
            record.ProductId !=
                requestedOrder.ProductId ||
            record.Quantity !=
                requestedOrder.Quantity)
        {
            throw new IdempotencyConflictException(
                "The Idempotency-Key has already been used " +
                "for a different request.");
        }

        var existingOrder =
            await dbContext.Orders
                .AsNoTracking()
                .SingleAsync(
                    existing =>
                        existing.TenantId ==
                            record.TenantId &&
                        existing.Id ==
                            record.OrderId,
                    cancellationToken);

        return new OrderWriteResult(
            existingOrder,
            false);
    }
}