using EuroTrade.Infrastructure.Persistence;
using EuroTrade.Infrastructure.Persistence.Inbox;

using Microsoft.EntityFrameworkCore;

namespace EuroTrade.Integration.Tests.Messaging;

[Collection("Postgres integration")]
public sealed class PostgresInboxConcurrencyTests(
    PostgresTestFixture postgres)
{
    [Fact]
    public async Task Concurrent_workers_record_same_message_only_once()
    {
        var connectionString =
            postgres.ConnectionString;

        var options =
            new DbContextOptionsBuilder<OrdersDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        await ResetDatabaseAsync(
            options);

        try
        {
            const string messageId =
                "same-message-id";

            var store =
                new InboxMessageStore();

            await using var contextA =
                new OrdersDbContext(
                    options);

            await using var contextB =
                new OrdersDbContext(
                    options);

            var workerA =
                store.TryRecordAsync(
                    contextA,
                    messageId);

            var workerB =
                store.TryRecordAsync(
                    contextB,
                    messageId);

            var results =
                await Task.WhenAll(
                    workerA,
                    workerB);

            Assert.Equal(
                1,
                results.Count(
                    result => result));

            Assert.Equal(
                1,
                results.Count(
                    result => !result));

            await using var verificationContext =
                new OrdersDbContext(
                    options);

            var inboxRows =
                await verificationContext
                    .InboxMessages
                    .AsNoTracking()
                    .Where(
                        message =>
                            message.MessageId ==
                            messageId)
                    .ToListAsync();

            Assert.Single(
                inboxRows);
        }
        finally
        {
            await ResetDatabaseAsync(
                options);
        }
    }

    [Fact]
    public async Task Repeated_concurrent_attempts_do_not_throw_unique_key_exception()
    {
        var connectionString =
            postgres.ConnectionString;

        var options =
            new DbContextOptionsBuilder<OrdersDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        await ResetDatabaseAsync(
            options);

        try
        {
            const string messageId =
                "concurrent-retry-message";

            var store =
                new InboxMessageStore();

            var tasks =
                Enumerable.Range(
                        0,
                        10)
                    .Select(
                        async _ =>
                        {
                            await using var context =
                                new OrdersDbContext(
                                    options);

                            return await store.TryRecordAsync(
                                context,
                                messageId);
                        })
                    .ToArray();

            var results =
                await Task.WhenAll(
                    tasks);

            Assert.Equal(
                1,
                results.Count(
                    result => result));

            Assert.Equal(
                9,
                results.Count(
                    result => !result));

            await using var verificationContext =
                new OrdersDbContext(
                    options);

            var count =
                await verificationContext
                    .InboxMessages
                    .AsNoTracking()
                    .CountAsync(
                        message =>
                            message.MessageId ==
                            messageId);

            Assert.Equal(
                1,
                count);
        }
        finally
        {
            await ResetDatabaseAsync(
                options);
        }
    }

    private static async Task ResetDatabaseAsync(
        DbContextOptions<OrdersDbContext> options)
    {
        await using var dbContext =
            new OrdersDbContext(
                options);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DROP SCHEMA IF EXISTS public CASCADE;
            CREATE SCHEMA public;
            """);

        await dbContext.Database
            .EnsureCreatedAsync();
    }
}