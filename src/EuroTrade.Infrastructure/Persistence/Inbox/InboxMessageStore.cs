using Microsoft.EntityFrameworkCore;

namespace EuroTrade.Infrastructure.Persistence.Inbox;

public sealed class InboxMessageStore
{
    public async Task<bool> TryRecordAsync(
        OrdersDbContext dbContext,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            messageId);

        var id =
            Guid.NewGuid();

        var now =
            DateTimeOffset.UtcNow;

        if (dbContext.Database.IsNpgsql())
        {
            var affectedRows =
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO inbox_messages
                        ("Id", "MessageId", "ReceivedAt", "ProcessedAt")
                    VALUES
                        ({id}, {messageId}, {now}, {now})
                    ON CONFLICT ("MessageId") DO NOTHING
                    """,
                    cancellationToken);

            return affectedRows == 1;
        }

        /*
         * SQLite is used by the E2E/local test path.
         *
         * INSERT OR IGNORE provides the same atomic
         * insert-if-absent behavior needed by the inbox.
         */
        if (dbContext.Database.IsSqlite())
        {
            var affectedRows =
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT OR IGNORE INTO inbox_messages
                        ("Id", "MessageId", "ReceivedAt", "ProcessedAt")
                    VALUES
                        ({id}, {messageId}, {now}, {now})
                    """,
                    cancellationToken);

            return affectedRows == 1;
        }

        /*
         * Provider-neutral fallback.
         *
         * Production PostgreSQL and test SQLite both use
         * atomic provider-specific statements above.
         */
        dbContext.InboxMessages.Add(
            new InboxMessage
            {
                Id = id,
                MessageId = messageId,
                ReceivedAt = now,
                ProcessedAt = now
            });

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken);

            return true;
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();

            var alreadyExists =
                await dbContext.InboxMessages
                    .AsNoTracking()
                    .AnyAsync(
                        inbox =>
                            inbox.MessageId == messageId,
                        cancellationToken);

            if (alreadyExists)
            {
                return false;
            }

            throw;
        }
    }
}