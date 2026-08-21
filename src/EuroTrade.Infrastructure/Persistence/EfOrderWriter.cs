using System.Text.Json;
using EuroTrade.Application.Orders;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Domain.Orders;
using EuroTrade.Infrastructure.Persistence.Outbox;

namespace EuroTrade.Infrastructure.Persistence;

public sealed class EfOrderWriter(
    OrdersDbContext dbContext) : IOrderWriter
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task AddAsync(
        Order order,
        OrderCreated orderCreated,
        CancellationToken cancellationToken = default)
    {
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = nameof(OrderCreated),
            Payload = JsonSerializer.Serialize(
                orderCreated,
                JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        dbContext.Orders.Add(order);
        dbContext.OutboxMessages.Add(outboxMessage);

        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
