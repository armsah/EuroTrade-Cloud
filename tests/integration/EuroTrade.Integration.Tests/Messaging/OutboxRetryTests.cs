using System.Text.Json;

using EuroTrade.Application.Messaging;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Infrastructure.Messaging;
using EuroTrade.Infrastructure.Persistence;
using EuroTrade.Infrastructure.Persistence.Outbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EuroTrade.Integration.Tests.Messaging;

public sealed class OutboxRetryTests
{
    [Fact]
    public async Task Transient_failure_records_retry_state()
    {
        await using var database =
            await SqliteTestDatabase.CreateAsync();

        var eventBus =
            new ConfigurableEventBus(
                failuresBeforeSuccess: int.MaxValue);

        using var services =
            BuildServices(
                database.Factory,
                eventBus,
                maxAttempts: 5,
                baseRetryDelaySeconds: 2,
                maxRetryDelaySeconds: 300);

        var messageId =
            await InsertPendingOrderCreatedAsync(
                database.Factory);

        var publisher =
            services.GetRequiredService<
                OutboxPublisher>();

        await publisher.PublishPendingMessagesAsync(
            CancellationToken.None);

        await using var verificationDb =
            await database.Factory.CreateDbContextAsync();

        var stored =
            await verificationDb.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.Id == messageId);

        Assert.Equal(
            1,
            stored.AttemptCount);

        Assert.NotNull(
            stored.LastAttemptAt);

        Assert.NotNull(
            stored.LastError);

        Assert.Contains(
            "Intentional transient publish failure",
            stored.LastError);

        Assert.NotNull(
            stored.NextAttemptAt);

        Assert.True(
            stored.NextAttemptAt >
            stored.LastAttemptAt);

        Assert.Null(
            stored.PublishedAt);

        Assert.Null(
            stored.FailedAt);

        Assert.Equal(
            1,
            eventBus.PublishCount);
    }

    [Fact]
    public async Task Message_is_not_retried_before_next_attempt_time()
    {
        await using var database =
            await SqliteTestDatabase.CreateAsync();

        var eventBus =
            new ConfigurableEventBus(
                failuresBeforeSuccess: int.MaxValue);

        using var services =
            BuildServices(
                database.Factory,
                eventBus,
                maxAttempts: 5,
                baseRetryDelaySeconds: 60,
                maxRetryDelaySeconds: 60);

        var messageId =
            await InsertPendingOrderCreatedAsync(
                database.Factory);

        var publisher =
            services.GetRequiredService<
                OutboxPublisher>();

        // First polling cycle fails and schedules a retry.
        await publisher.PublishPendingMessagesAsync(
            CancellationToken.None);

        Assert.Equal(
            1,
            eventBus.PublishCount);

        // Second polling cycle happens immediately.
        // NextAttemptAt is still in the future, so this
        // message must not be selected again.
        await publisher.PublishPendingMessagesAsync(
            CancellationToken.None);

        Assert.Equal(
            1,
            eventBus.PublishCount);

        await using var verificationDb =
            await database.Factory.CreateDbContextAsync();

        var stored =
            await verificationDb.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.Id == messageId);

        Assert.Equal(
            1,
            stored.AttemptCount);

        Assert.NotNull(
            stored.LastAttemptAt);

        Assert.NotNull(
            stored.NextAttemptAt);

        Assert.True(
            stored.NextAttemptAt >
            DateTimeOffset.UtcNow);

        Assert.NotNull(
            stored.LastError);

        Assert.Null(
            stored.PublishedAt);

        Assert.Null(
            stored.FailedAt);
    }

    [Fact]
    public async Task Successful_retry_clears_retry_state_and_publishes_message()
    {
        await using var database =
            await SqliteTestDatabase.CreateAsync();

        var eventBus =
            new ConfigurableEventBus(
                failuresBeforeSuccess: 1);

        using var services =
            BuildServices(
                database.Factory,
                eventBus,
                maxAttempts: 5,
                baseRetryDelaySeconds: 60,
                maxRetryDelaySeconds: 60);

        var messageId =
            await InsertPendingOrderCreatedAsync(
                database.Factory);

        var publisher =
            services.GetRequiredService<
                OutboxPublisher>();

        // Attempt 1 fails.
        await publisher.PublishPendingMessagesAsync(
            CancellationToken.None);

        await using (var db =
            await database.Factory.CreateDbContextAsync())
        {
            var failedAttempt =
                await db.OutboxMessages
                    .SingleAsync(
                        message =>
                            message.Id == messageId);

            Assert.Equal(
                1,
                failedAttempt.AttemptCount);

            Assert.NotNull(
                failedAttempt.NextAttemptAt);

            Assert.NotNull(
                failedAttempt.LastError);

            // Make the retry immediately eligible instead of
            // sleeping for the configured backoff period.
            failedAttempt.NextAttemptAt =
                DateTimeOffset.UtcNow
                    .AddSeconds(-1);

            await db.SaveChangesAsync();
        }

        // Attempt 2 succeeds.
        await publisher.PublishPendingMessagesAsync(
            CancellationToken.None);

        await using var verificationDb =
            await database.Factory.CreateDbContextAsync();

        var stored =
            await verificationDb.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.Id == messageId);

        Assert.Equal(
            2,
            stored.AttemptCount);

        Assert.NotNull(
            stored.LastAttemptAt);

        Assert.NotNull(
            stored.PublishedAt);

        Assert.Null(
            stored.LastError);

        Assert.Null(
            stored.NextAttemptAt);

        Assert.Null(
            stored.FailedAt);

        Assert.Equal(
            2,
            eventBus.PublishCount);
    }

    [Fact]
    public async Task Max_attempts_moves_message_to_poison_state()
    {
        await using var database =
            await SqliteTestDatabase.CreateAsync();

        var eventBus =
            new ConfigurableEventBus(
                failuresBeforeSuccess: int.MaxValue);

        using var services =
            BuildServices(
                database.Factory,
                eventBus,
                maxAttempts: 3,
                baseRetryDelaySeconds: 60,
                maxRetryDelaySeconds: 60);

        var messageId =
            await InsertPendingOrderCreatedAsync(
                database.Factory);

        var publisher =
            services.GetRequiredService<
                OutboxPublisher>();

        // Attempt 1.
        await publisher.PublishPendingMessagesAsync(
            CancellationToken.None);

        await MakeRetryImmediatelyEligibleAsync(
            database.Factory,
            messageId);

        // Attempt 2.
        await publisher.PublishPendingMessagesAsync(
            CancellationToken.None);

        await MakeRetryImmediatelyEligibleAsync(
            database.Factory,
            messageId);

        // Attempt 3 reaches MaxAttempts and poisons
        // the message.
        await publisher.PublishPendingMessagesAsync(
            CancellationToken.None);

        await using (var verificationDb =
            await database.Factory.CreateDbContextAsync())
        {
            var stored =
                await verificationDb.OutboxMessages
                    .AsNoTracking()
                    .SingleAsync(
                        message =>
                            message.Id == messageId);

            Assert.Equal(
                3,
                stored.AttemptCount);

            Assert.NotNull(
                stored.LastAttemptAt);

            Assert.NotNull(
                stored.LastError);

            Assert.Contains(
                "Intentional transient publish failure",
                stored.LastError);

            Assert.NotNull(
                stored.FailedAt);

            Assert.Null(
                stored.NextAttemptAt);

            Assert.Null(
                stored.PublishedAt);
        }

        Assert.Equal(
            3,
            eventBus.PublishCount);

        // A poisoned message must no longer be selected.
        await publisher.PublishPendingMessagesAsync(
            CancellationToken.None);

        Assert.Equal(
            3,
            eventBus.PublishCount);
    }

    [Fact]
    public async Task Unsupported_message_type_is_failed_immediately()
    {
        await using var database =
            await SqliteTestDatabase.CreateAsync();

        var eventBus =
            new ConfigurableEventBus(
                failuresBeforeSuccess: 0);

        using var services =
            BuildServices(
                database.Factory,
                eventBus,
                maxAttempts: 5,
                baseRetryDelaySeconds: 2,
                maxRetryDelaySeconds: 300);

        var messageId =
            Guid.NewGuid();

        await using (var db =
            await database.Factory.CreateDbContextAsync())
        {
            db.OutboxMessages.Add(
                new OutboxMessage
                {
                    Id =
                        messageId,

                    MessageType =
                        "UnsupportedEvent",

                    Payload =
                        "{}",

                    CreatedAt =
                        DateTimeOffset.UtcNow
                });

            await db.SaveChangesAsync();
        }

        var publisher =
            services.GetRequiredService<
                OutboxPublisher>();

        await publisher.PublishPendingMessagesAsync(
            CancellationToken.None);

        await using var verificationDb =
            await database.Factory.CreateDbContextAsync();

        var stored =
            await verificationDb.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.Id == messageId);

        Assert.Equal(
            1,
            stored.AttemptCount);

        Assert.NotNull(
            stored.LastAttemptAt);

        Assert.NotNull(
            stored.FailedAt);

        Assert.Null(
            stored.NextAttemptAt);

        Assert.Null(
            stored.PublishedAt);

        Assert.NotNull(
            stored.LastError);

        Assert.Contains(
            "Unsupported outbox message type",
            stored.LastError);

        // The event bus should never be called because the
        // message type is permanently invalid.
        Assert.Equal(
            0,
            eventBus.PublishCount);

        // Confirm poison state prevents future attempts.
        await publisher.PublishPendingMessagesAsync(
            CancellationToken.None);

        Assert.Equal(
            0,
            eventBus.PublishCount);
    }

    private static ServiceProvider BuildServices(
        IDbContextFactory<OrdersDbContext> factory,
        ConfigurableEventBus eventBus,
        int maxAttempts,
        double baseRetryDelaySeconds,
        double maxRetryDelaySeconds)
    {
        var configurationValues =
            new Dictionary<string, string?>
            {
                ["Outbox:MaxAttempts"] =
                    maxAttempts.ToString(),

                ["Outbox:BaseRetryDelaySeconds"] =
                    baseRetryDelaySeconds.ToString(
                        System.Globalization
                            .CultureInfo.InvariantCulture),

                ["Outbox:MaxRetryDelaySeconds"] =
                    maxRetryDelaySeconds.ToString(
                        System.Globalization
                            .CultureInfo.InvariantCulture)
            };

        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    configurationValues)
                .Build();

        var services =
            new ServiceCollection();

        services.AddSingleton<
            IDbContextFactory<OrdersDbContext>>(
            factory);

        /*
         * OutboxPublisher resolves OrdersDbContext from a
         * scope. Each scope therefore receives its own
         * context from the shared test factory.
         */
        services.AddScoped<OrdersDbContext>(
            provider =>
                provider
                    .GetRequiredService<
                        IDbContextFactory<
                            OrdersDbContext>>()
                    .CreateDbContext());

        services.AddSingleton(
            eventBus);

        services.AddSingleton<IEventBus>(
            provider =>
                provider.GetRequiredService<
                    ConfigurableEventBus>());

        services.AddSingleton<IConfiguration>(
            configuration);

        services.AddSingleton<
            ILogger<OutboxPublisher>>(
            NullLogger<OutboxPublisher>.Instance);

        services.AddSingleton<
            OutboxPublisher>();

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes =
                    true,

                ValidateOnBuild =
                    true
            });
    }

    private static async Task<Guid>
        InsertPendingOrderCreatedAsync(
            IDbContextFactory<OrdersDbContext> factory)
    {
        var orderId =
            Guid.NewGuid();

        var domainEvent =
            new OrderCreated(
                orderId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                5,
                DateTimeOffset.UtcNow);

        var messageId =
            Guid.NewGuid();

        await using var db =
            await factory.CreateDbContextAsync();

        db.OutboxMessages.Add(
            new OutboxMessage
            {
                Id =
                    messageId,

                MessageType =
                    nameof(OrderCreated),

                Payload =
                    JsonSerializer.Serialize(
                        domainEvent),

                CreatedAt =
                    DateTimeOffset.UtcNow
            });

        await db.SaveChangesAsync();

        return messageId;
    }

    private static async Task
        MakeRetryImmediatelyEligibleAsync(
            IDbContextFactory<OrdersDbContext> factory,
            Guid messageId)
    {
        await using var db =
            await factory.CreateDbContextAsync();

        var message =
            await db.OutboxMessages
                .SingleAsync(
                    item =>
                        item.Id == messageId);

        Assert.NotNull(
            message.NextAttemptAt);

        Assert.Null(
            message.FailedAt);

        message.NextAttemptAt =
            DateTimeOffset.UtcNow
                .AddSeconds(-1);

        await db.SaveChangesAsync();
    }

    private sealed class ConfigurableEventBus(
        int failuresBeforeSuccess)
        : IEventBus
    {
        private int _remainingFailures =
            failuresBeforeSuccess;

        private int _publishCount;

        public int PublishCount =>
            Volatile.Read(
                ref _publishCount);

        public Task PublishAsync<T>(
            T message,
            string? messageId = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(
                ref _publishCount);

            if (Interlocked.CompareExchange(
                    ref _remainingFailures,
                    0,
                    0) > 0)
            {
                Interlocked.Decrement(
                    ref _remainingFailures);

                throw new InvalidOperationException(
                    "Intentional transient publish failure.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class SqliteTestDatabase
        : IAsyncDisposable
    {
        private readonly Microsoft.Data.Sqlite
            .SqliteConnection _connection;

        private SqliteTestDatabase(
            Microsoft.Data.Sqlite.SqliteConnection connection,
            IDbContextFactory<OrdersDbContext> factory)
        {
            _connection =
                connection;

            Factory =
                factory;
        }

        public IDbContextFactory<OrdersDbContext> Factory
        {
            get;
        }

        public static async Task<SqliteTestDatabase>
            CreateAsync()
        {
            var connection =
                new Microsoft.Data.Sqlite
                    .SqliteConnection(
                        "Data Source=:memory:");

            await connection.OpenAsync();

            var options =
                new DbContextOptionsBuilder<
                    OrdersDbContext>()
                    .UseSqlite(
                        connection)
                    .Options;

            var factory =
                new TestDbContextFactory(
                    options);

            await using var db =
                await factory.CreateDbContextAsync();

            await db.Database
                .EnsureCreatedAsync();

            return new SqliteTestDatabase(
                connection,
                factory);
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<OrdersDbContext> options)
        : IDbContextFactory<OrdersDbContext>
    {
        public OrdersDbContext CreateDbContext()
        {
            return new OrdersDbContext(
                options);
        }

        public Task<OrdersDbContext>
            CreateDbContextAsync(
                CancellationToken cancellationToken =
                    default)
        {
            return Task.FromResult(
                CreateDbContext());
        }
    }
}