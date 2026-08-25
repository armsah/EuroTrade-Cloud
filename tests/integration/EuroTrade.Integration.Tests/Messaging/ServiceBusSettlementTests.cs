using System.Text.Json;

using Azure.Messaging.ServiceBus;

using EuroTrade.Application.Orders.Events;

using EuroTrade.Infrastructure.Messaging;
using EuroTrade.Infrastructure.Persistence;
using EuroTrade.Infrastructure.Persistence.Inbox;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace EuroTrade.Integration.Tests.Messaging;

public sealed class ServiceBusSettlementTests
{
    [Fact]
    public async Task Successfully_processed_message_is_completed()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        var serviceBusProcessor =
            new TestServiceBusProcessor();

        var configuration =
            CreateConfiguration(
                forceFailure: false);

        var hostedProcessor =
            new AzureServiceBusOrderProcessor(
                serviceBusProcessor,
                database.Factory,
                configuration,
                NullLogger<
                    AzureServiceBusOrderProcessor>.Instance);

        await hostedProcessor.StartAsync(
            CancellationToken.None);

        var receiver =
            new RecordingServiceBusReceiver();

        var message =
            CreateOrderCreatedMessage(
                "success-message");

        var args =
            new ProcessMessageEventArgs(
                message,
                receiver,
                "test-processor",
                CancellationToken.None);

        await serviceBusProcessor
            .EmitMessageAsync(
                args);

        Assert.Equal(
            1,
            receiver.CompletedCount);

        Assert.Equal(
            0,
            receiver.AbandonedCount);

        Assert.Equal(
            0,
            receiver.DeadLetteredCount);

        var inboxExists =
            await database.ExecuteAsync(
                db =>
                    db.InboxMessages.AnyAsync(
                        inbox =>
                            inbox.MessageId ==
                            message.MessageId));

        Assert.True(
            inboxExists);

        await hostedProcessor.StopAsync(
            CancellationToken.None);
    }

    [Fact]
    public async Task Duplicate_message_is_completed()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        const string messageId =
            "duplicate-message";

        await database.ExecuteAsync(
            async db =>
            {
                db.InboxMessages.Add(
                    new InboxMessage
                    {
                        Id =
                            Guid.NewGuid(),

                        MessageId =
                            messageId,

                        ReceivedAt =
                            DateTimeOffset.UtcNow,

                        ProcessedAt =
                            DateTimeOffset.UtcNow
                    });

                await db.SaveChangesAsync();
            });

        var serviceBusProcessor =
            new TestServiceBusProcessor();

        var hostedProcessor =
            new AzureServiceBusOrderProcessor(
                serviceBusProcessor,
                database.Factory,
                CreateConfiguration(false),
                NullLogger<
                    AzureServiceBusOrderProcessor>.Instance);

        await hostedProcessor.StartAsync(
            CancellationToken.None);

        var receiver =
            new RecordingServiceBusReceiver();

        var message =
            CreateOrderCreatedMessage(
                messageId);

        var args =
            new ProcessMessageEventArgs(
                message,
                receiver,
                "test-processor",
                CancellationToken.None);

        await serviceBusProcessor
            .EmitMessageAsync(
                args);

        Assert.Equal(
            1,
            receiver.CompletedCount);

        Assert.Equal(
            0,
            receiver.AbandonedCount);

        Assert.Equal(
            0,
            receiver.DeadLetteredCount);

        await hostedProcessor.StopAsync(
            CancellationToken.None);
    }

    [Fact]
    public async Task Processing_failure_abandons_message()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        var serviceBusProcessor =
            new TestServiceBusProcessor();

        var hostedProcessor =
            new AzureServiceBusOrderProcessor(
                serviceBusProcessor,
                database.Factory,
                CreateConfiguration(true),
                NullLogger<
                    AzureServiceBusOrderProcessor>.Instance);

        await hostedProcessor.StartAsync(
            CancellationToken.None);

        var receiver =
            new RecordingServiceBusReceiver();

        var message =
            CreateOrderCreatedMessage(
                "failure-message");

        var args =
            new ProcessMessageEventArgs(
                message,
                receiver,
                "test-processor",
                CancellationToken.None);

        await serviceBusProcessor
            .EmitMessageAsync(
                args);

        Assert.Equal(
            0,
            receiver.CompletedCount);

        Assert.Equal(
            1,
            receiver.AbandonedCount);

        Assert.Equal(
            0,
            receiver.DeadLetteredCount);

        var inboxExists =
            await database.ExecuteAsync(
                db =>
                    db.InboxMessages.AnyAsync(
                        inbox =>
                            inbox.MessageId ==
                            message.MessageId));

        Assert.False(
            inboxExists);

        await hostedProcessor.StopAsync(
            CancellationToken.None);
    }

    [Fact]
    public async Task Malformed_message_is_dead_lettered()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        var serviceBusProcessor =
            new TestServiceBusProcessor();

        var hostedProcessor =
            new AzureServiceBusOrderProcessor(
                serviceBusProcessor,
                database.Factory,
                CreateConfiguration(false),
                NullLogger<
                    AzureServiceBusOrderProcessor>.Instance);

        await hostedProcessor.StartAsync(
            CancellationToken.None);

        var receiver =
            new RecordingServiceBusReceiver();

        var message =
            ServiceBusModelFactory
                .ServiceBusReceivedMessage(
                    body:
                        BinaryData.FromString(
                            "{ invalid-json "),
                    messageId:
                        "malformed-message",
                    subject:
                        nameof(OrderCreated),
                    properties:
                        new Dictionary<string, object>
                        {
                            ["eventType"] =
                                nameof(OrderCreated)
                        });

        var args =
            new ProcessMessageEventArgs(
                message,
                receiver,
                "test-processor",
                CancellationToken.None);

        await serviceBusProcessor
            .EmitMessageAsync(
                args);

        Assert.Equal(
            0,
            receiver.CompletedCount);

        Assert.Equal(
            0,
            receiver.AbandonedCount);

        Assert.Equal(
            1,
            receiver.DeadLetteredCount);

        Assert.Equal(
            "InvalidEventPayload",
            receiver.LastDeadLetterReason);

        await hostedProcessor.StopAsync(
            CancellationToken.None);
    }

    [Fact]
    public async Task Unknown_event_type_is_dead_lettered()
    {
        await using var database =
            await TestDatabase.CreateAsync();

        var serviceBusProcessor =
            new TestServiceBusProcessor();

        var hostedProcessor =
            new AzureServiceBusOrderProcessor(
                serviceBusProcessor,
                database.Factory,
                CreateConfiguration(false),
                NullLogger<
                    AzureServiceBusOrderProcessor>.Instance);

        await hostedProcessor.StartAsync(
            CancellationToken.None);

        var receiver =
            new RecordingServiceBusReceiver();

        var message =
            ServiceBusModelFactory
                .ServiceBusReceivedMessage(
                    body:
                        BinaryData.FromString(
                            "{}"),
                    messageId:
                        "unknown-message",
                    subject:
                        "UnexpectedEvent",
                    properties:
                        new Dictionary<string, object>
                        {
                            ["eventType"] =
                                "UnexpectedEvent"
                        });

        var args =
            new ProcessMessageEventArgs(
                message,
                receiver,
                "test-processor",
                CancellationToken.None);

        await serviceBusProcessor
            .EmitMessageAsync(
                args);

        Assert.Equal(
            0,
            receiver.CompletedCount);

        Assert.Equal(
            0,
            receiver.AbandonedCount);

        Assert.Equal(
            1,
            receiver.DeadLetteredCount);

        Assert.Equal(
            "UnknownEventType",
            receiver.LastDeadLetterReason);

        await hostedProcessor.StopAsync(
            CancellationToken.None);
    }

    private static IConfiguration
        CreateConfiguration(
            bool forceFailure)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ServiceBus:ForceProcessingFailure"] =
                        forceFailure.ToString()
                })
            .Build();
    }

    private static ServiceBusReceivedMessage
        CreateOrderCreatedMessage(
            string messageId)
    {
        var orderCreated =
            new OrderCreated(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                2,
                DateTimeOffset.UtcNow);

        var payload =
            JsonSerializer.Serialize(
                orderCreated,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web));

        return ServiceBusModelFactory
            .ServiceBusReceivedMessage(
                body:
                    BinaryData.FromString(
                        payload),
                messageId:
                    messageId,
                subject:
                    nameof(OrderCreated),
                properties:
                    new Dictionary<string, object>
                    {
                        ["eventType"] =
                            nameof(OrderCreated)
                    });
    }

    private sealed class TestServiceBusProcessor
        : ServiceBusProcessor
    {
        public bool Started
        {
            get;
            private set;
        }

        public override Task StartProcessingAsync(
            CancellationToken cancellationToken =
                default)
        {
            Started = true;

            return Task.CompletedTask;
        }

        public override Task StopProcessingAsync(
            CancellationToken cancellationToken =
                default)
        {
            Started = false;

            return Task.CompletedTask;
        }

        public Task EmitMessageAsync(
            ProcessMessageEventArgs args)
        {
            return OnProcessMessageAsync(
                args);
        }
    }

    private sealed class RecordingServiceBusReceiver
        : ServiceBusReceiver
    {
        public int CompletedCount
        {
            get;
            private set;
        }

        public int AbandonedCount
        {
            get;
            private set;
        }

        public int DeadLetteredCount
        {
            get;
            private set;
        }

        public string? LastDeadLetterReason
        {
            get;
            private set;
        }

        public override Task CompleteMessageAsync(
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken =
                default)
        {
            CompletedCount++;

            return Task.CompletedTask;
        }

        public override Task AbandonMessageAsync(
            ServiceBusReceivedMessage message,
            IDictionary<string, object>?
                propertiesToModify = null,
            CancellationToken cancellationToken =
                default)
        {
            AbandonedCount++;

            return Task.CompletedTask;
        }

        public override Task DeadLetterMessageAsync(
            ServiceBusReceivedMessage message,
            string deadLetterReason,
            string? deadLetterErrorDescription =
                null,
            CancellationToken cancellationToken =
                default)
        {
            DeadLetteredCount++;

            LastDeadLetterReason =
                deadLetterReason;

            return Task.CompletedTask;
        }
    }

    private sealed class TestDatabase
        : IAsyncDisposable
    {
        private readonly SqliteConnection
            _connection;

        private readonly DbContextOptions<
            OrdersDbContext> _options;

        private TestDatabase(
            SqliteConnection connection,
            DbContextOptions<OrdersDbContext>
                options)
        {
            _connection =
                connection;

            _options =
                options;

            Factory =
                new TestDbContextFactory(
                    options);
        }

        public IDbContextFactory<OrdersDbContext>
            Factory
        { get; }

        public static async Task<TestDatabase>
            CreateAsync()
        {
            var connection =
                new SqliteConnection(
                    "Data Source=:memory:");

            await connection.OpenAsync();

            var options =
                new DbContextOptionsBuilder<
                    OrdersDbContext>()
                    .UseSqlite(
                        connection)
                    .Options;

            var result =
                new TestDatabase(
                    connection,
                    options);

            await result.ExecuteAsync(
                db =>
                    db.Database.EnsureCreatedAsync());

            return result;
        }

        public async Task ExecuteAsync(
            Func<OrdersDbContext, Task>
                operation)
        {
            await using var db =
                new OrdersDbContext(
                    _options);

            await operation(
                db);
        }

        public async Task<T> ExecuteAsync<T>(
            Func<OrdersDbContext, Task<T>>
                operation)
        {
            await using var db =
                new OrdersDbContext(
                    _options);

            return await operation(
                db);
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
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                new OrdersDbContext(
                    options));
        }
    }
}