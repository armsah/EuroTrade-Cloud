using EuroTrade.Application.Orders.Events;

using EuroTrade.Infrastructure.Persistence;
using EuroTrade.Infrastructure.Persistence.Outbox;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace EuroTrade.Integration.Tests.Messaging;

public sealed class PostgresOutboxConcurrencyTests
{
    private const string ConnectionStringEnvironmentVariable =
        "EUROTRADE_TEST_POSTGRES";

    [Fact]
    public async Task Skip_locked_prevents_two_transactions_from_claiming_same_rows()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                ConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // PostgreSQL-specific test.
            // Normal dotnet test runs remain portable when PostgreSQL
            // is not available locally.
            return;
        }

        var options =
            new DbContextOptionsBuilder<OrdersDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        await ResetDatabaseAsync(options);

        try
        {
            var firstMessageId =
                Guid.NewGuid();

            var secondMessageId =
                Guid.NewGuid();

            await SeedOutboxAsync(
                options,
                firstMessageId,
                secondMessageId);

            await using var contextA =
                new OrdersDbContext(options);

            await using var contextB =
                new OrdersDbContext(options);

            await using var transactionA =
                await contextA.Database.BeginTransactionAsync();

            // Transaction A claims the oldest message and keeps
            // the row lock because transactionA remains open.
            var batchA =
                await contextA.OutboxMessages
                    .FromSqlRaw(
                        """
                        SELECT *
                        FROM outbox_messages
                        WHERE "PublishedAt" IS NULL
                        ORDER BY "CreatedAt"
                        LIMIT 1
                        FOR UPDATE SKIP LOCKED
                        """)
                    .AsTracking()
                    .ToListAsync();

            Assert.Single(batchA);

            Assert.Equal(
                firstMessageId,
                batchA[0].Id);

            // While transaction A still owns its row lock,
            // transaction B performs the same claiming query.
            await using var transactionB =
                await contextB.Database.BeginTransactionAsync();

            var batchB =
                await contextB.OutboxMessages
                    .FromSqlRaw(
                        """
                        SELECT *
                        FROM outbox_messages
                        WHERE "PublishedAt" IS NULL
                        ORDER BY "CreatedAt"
                        LIMIT 1
                        FOR UPDATE SKIP LOCKED
                        """)
                    .AsTracking()
                    .ToListAsync();

            Assert.Single(batchB);

            // The critical assertion:
            // transaction B did not receive transaction A's row.
            Assert.NotEqual(
                batchA[0].Id,
                batchB[0].Id);

            Assert.Equal(
                secondMessageId,
                batchB[0].Id);

            await transactionB.RollbackAsync();
            await transactionA.RollbackAsync();
        }
        finally
        {
            await ResetDatabaseAsync(options);
        }
    }

    [Fact]
    public async Task Skip_locked_allows_second_transaction_to_claim_remaining_batch()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                ConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var options =
            new DbContextOptionsBuilder<OrdersDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        await ResetDatabaseAsync(options);

        try
        {
            var messageIds =
                Enumerable.Range(0, 4)
                    .Select(_ => Guid.NewGuid())
                    .ToArray();

            await SeedOutboxAsync(
                options,
                messageIds);

            await using var contextA =
                new OrdersDbContext(options);

            await using var contextB =
                new OrdersDbContext(options);

            await using var transactionA =
                await contextA.Database.BeginTransactionAsync();

            var batchA =
                await contextA.OutboxMessages
                    .FromSqlRaw(
                        """
                        SELECT *
                        FROM outbox_messages
                        WHERE "PublishedAt" IS NULL
                        ORDER BY "CreatedAt"
                        LIMIT 2
                        FOR UPDATE SKIP LOCKED
                        """)
                    .AsTracking()
                    .ToListAsync();

            Assert.Equal(
                2,
                batchA.Count);

            await using var transactionB =
                await contextB.Database.BeginTransactionAsync();

            var batchB =
                await contextB.OutboxMessages
                    .FromSqlRaw(
                        """
                        SELECT *
                        FROM outbox_messages
                        WHERE "PublishedAt" IS NULL
                        ORDER BY "CreatedAt"
                        LIMIT 2
                        FOR UPDATE SKIP LOCKED
                        """)
                    .AsTracking()
                    .ToListAsync();

            Assert.Equal(
                2,
                batchB.Count);

            var batchAIds =
                batchA
                    .Select(message => message.Id)
                    .ToHashSet();

            var batchBIds =
                batchB
                    .Select(message => message.Id)
                    .ToHashSet();

            // No row may be claimed by both transactions.
            Assert.Empty(
                batchAIds.Intersect(batchBIds));

            // Together, both replicas claimed all four rows.
            Assert.Equal(
                4,
                batchAIds
                    .Union(batchBIds)
                    .Count());

            await transactionB.RollbackAsync();
            await transactionA.RollbackAsync();
        }
        finally
        {
            await ResetDatabaseAsync(options);
        }
    }

    private static async Task SeedOutboxAsync(
        DbContextOptions<OrdersDbContext> options,
        params Guid[] messageIds)
    {
        await using var dbContext =
            new OrdersDbContext(options);

        var baseTime =
            DateTimeOffset.UtcNow;

        for (var index = 0;
             index < messageIds.Length;
             index++)
        {
            var orderCreated =
                new OrderCreated(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    1,
                    baseTime.AddMilliseconds(index));

            dbContext.OutboxMessages.Add(
                new OutboxMessage
                {
                    Id =
                        messageIds[index],

                    MessageType =
                        nameof(OrderCreated),

                    Payload =
                        System.Text.Json.JsonSerializer.Serialize(
                            orderCreated),

                    CreatedAt =
                        baseTime.AddMilliseconds(index),

                    PublishedAt =
                        null,

                    Error =
                        null
                });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task ResetDatabaseAsync(
        DbContextOptions<OrdersDbContext> options)
    {
        await using var dbContext =
            new OrdersDbContext(options);

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }
}