using System.Diagnostics;
using System.Text.Json;

using EuroTrade.Application.Messaging;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Application.Telemetry;

using EuroTrade.Infrastructure.Persistence;
using EuroTrade.Infrastructure.Persistence.Outbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EuroTrade.Infrastructure.Messaging;

public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxPublisher> logger)
    : BackgroundService
{
    private const int BatchSize = 20;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Outbox publisher started.");

        using var timer =
            new PeriodicTimer(
                TimeSpan.FromSeconds(2));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingMessagesAsync(
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Error while publishing outbox messages.");
            }

            try
            {
                await timer.WaitForNextTickAsync(
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation(
            "Outbox publisher stopped.");
    }

    private async Task PublishPendingMessagesAsync(
        CancellationToken cancellationToken)
    {
        using var scope =
            scopeFactory.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<OrdersDbContext>();

        var eventBus =
            scope.ServiceProvider
                .GetRequiredService<IEventBus>();

        if (dbContext.Database.IsNpgsql())
        {
            await PublishPostgresBatchAsync(
                dbContext,
                eventBus,
                cancellationToken);

            return;
        }

        await PublishNonPostgresBatchAsync(
            dbContext,
            eventBus,
            cancellationToken);
    }

    private async Task PublishPostgresBatchAsync(
        OrdersDbContext dbContext,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            /*
             * The transaction is intentionally kept open while the
             * selected messages are published.
             *
             * FOR UPDATE:
             *     obtains row-level locks for this publisher.
             *
             * SKIP LOCKED:
             *     allows another application replica to immediately
             *     skip this publisher's batch and claim different rows.
             *
             * This prevents healthy replicas from concurrently
             * publishing the same outbox rows.
             */
            var messages =
                await dbContext.OutboxMessages
                    .FromSqlInterpolated(
                        $"""
                        SELECT *
                        FROM outbox_messages
                        WHERE "PublishedAt" IS NULL
                        ORDER BY "CreatedAt"
                        LIMIT {BatchSize}
                        FOR UPDATE SKIP LOCKED
                        """)
                    .AsTracking()
                    .ToListAsync(
                        cancellationToken);

            if (messages.Count == 0)
            {
                await transaction.CommitAsync(
                    cancellationToken);

                return;
            }

            logger.LogDebug(
                "Claimed {MessageCount} PostgreSQL " +
                "outbox messages using FOR UPDATE SKIP LOCKED.",
                messages.Count);

            await PublishMessagesAsync(
                dbContext,
                eventBus,
                messages,
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);
        }
        catch
        {
            await RollbackSafelyAsync(
                transaction,
                cancellationToken);

            throw;
        }
    }

    private async Task PublishNonPostgresBatchAsync(
    OrdersDbContext dbContext,
    IEventBus eventBus,
    CancellationToken cancellationToken)
    {
        /*
         * SQLite is used by local/E2E tests and does not support
         * PostgreSQL's FOR UPDATE SKIP LOCKED syntax.
         *
         * SQLite also cannot translate DateTimeOffset ORDER BY,
         * so rows are loaded first and ordered in memory.
         *
         * Production PostgreSQL uses the transactional claiming
         * path in PublishPostgresBatchAsync.
         */
        var pendingMessages =
            await dbContext.OutboxMessages
                .Where(message =>
                    message.PublishedAt == null)
                .ToListAsync(
                    cancellationToken);

        var messages =
            pendingMessages
                .OrderBy(message =>
                    message.CreatedAt)
                .Take(BatchSize)
                .ToList();

        await PublishMessagesAsync(
            dbContext,
            eventBus,
            messages,
            cancellationToken);
    }

    private async Task PublishMessagesAsync(
        OrdersDbContext dbContext,
        IEventBus eventBus,
        IReadOnlyCollection<OutboxMessage> messages,
        CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            try
            {
                await PublishMessageAsync(
                    dbContext,
                    eventBus,
                    message,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                message.Error =
                    exception.Message;

                await dbContext.SaveChangesAsync(
                    cancellationToken);

                logger.LogError(
                    exception,
                    "Failed to publish outbox message " +
                    "{MessageId}. It will be retried.",
                    message.Id);
            }
        }
    }

    private async Task PublishMessageAsync(
        OrdersDbContext dbContext,
        IEventBus eventBus,
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        switch (message.MessageType)
        {
            case nameof(OrderCreated):
                {
                    var orderCreated =
                        JsonSerializer.Deserialize<OrderCreated>(
                            message.Payload,
                            JsonOptions)
                        ?? throw new InvalidOperationException(
                            $"Could not deserialize outbox " +
                            $"message {message.Id}.");

                    var parentContext =
                        CreateParentContext(
                            message);

                    using var activity =
                        EuroTradeActivitySource.Source.StartActivity(
                            "PublishOrderCreated",
                            ActivityKind.Producer,
                            parentContext);

                    activity?.SetTag(
                        "messaging.system",
                        "azure_service_bus");

                    activity?.SetTag(
                        "messaging.destination.name",
                        "order-created");

                    activity?.SetTag(
                        "messaging.message.id",
                        message.Id.ToString());

                    activity?.SetTag(
                        "order.id",
                        orderCreated.OrderId);

                    activity?.SetTag(
                        "order.tenant_id",
                        orderCreated.TenantId);

                    try
                    {
                        await eventBus.PublishAsync(
                            orderCreated,
                            message.Id.ToString(),
                            cancellationToken);

                        message.PublishedAt =
                            DateTimeOffset.UtcNow;

                        message.Error =
                            null;

                        await dbContext.SaveChangesAsync(
                            cancellationToken);

                        activity?.SetStatus(
                            ActivityStatusCode.Ok);

                        logger.LogInformation(
                            "Published outbox message {MessageId}. " +
                            "MessageType: {MessageType}. " +
                            "OrderId: {OrderId}",
                            message.Id,
                            message.MessageType,
                            orderCreated.OrderId);
                    }
                    catch (Exception exception)
                    {
                        activity?.SetStatus(
                            ActivityStatusCode.Error,
                            exception.Message);

                        throw;
                    }

                    break;
                }

            default:
                {
                    message.Error =
                        $"Unsupported outbox message type: " +
                        $"{message.MessageType}";

                    await dbContext.SaveChangesAsync(
                        cancellationToken);

                    logger.LogError(
                        "Unsupported outbox message type " +
                        "{MessageType}. OutboxMessageId: {MessageId}",
                        message.MessageType,
                        message.Id);

                    break;
                }
        }
    }

    private static async Task RollbackSafelyAsync(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(
                cancellationToken);
        }
        catch
        {
            /*
             * Preserve the original publishing/database exception.
             * A rollback failure must not replace the exception that
             * caused the transaction to fail.
             */
        }
    }

    private static ActivityContext CreateParentContext(
        OutboxMessage message)
    {
        if (!string.IsNullOrWhiteSpace(
                message.TraceParent))
        {
            if (ActivityContext.TryParse(
                    message.TraceParent,
                    message.TraceState,
                    out var parentContext))
            {
                return parentContext;
            }
        }

        return default;
    }
}