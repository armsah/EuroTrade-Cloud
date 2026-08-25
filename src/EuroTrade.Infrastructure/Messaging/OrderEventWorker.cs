using System.Diagnostics;

using EuroTrade.Application.Messaging;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Application.Telemetry;

using EuroTrade.Infrastructure.Persistence;
using EuroTrade.Infrastructure.Persistence.Inbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EuroTrade.Infrastructure.Messaging;

public sealed class OrderEventWorker(
    IEventConsumer eventConsumer,
    IDbContextFactory<OrdersDbContext> dbContextFactory,
    InboxMessageStore inboxMessageStore,
    ILogger<OrderEventWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await foreach (
            var message in eventConsumer.ReadAllAsync(
                stoppingToken))
        {
            switch (message)
            {
                case InMemoryEventBus.InMemoryPublishedEvent
                    inMemoryEvent:

                    await ProcessInMemoryEventAsync(
                        inMemoryEvent,
                        stoppingToken);

                    break;

                case OrderCreated orderCreated:

                    // Backwards-compatible fallback for direct
                    // in-memory OrderCreated messages.
                    logger.LogInformation(
                        "OrderCreated event consumed in memory. " +
                        "OrderId: {OrderId}, TenantId: {TenantId}",
                        orderCreated.OrderId,
                        orderCreated.TenantId);

                    break;

                default:

                    logger.LogWarning(
                        "Unsupported in-memory event type " +
                        "{EventType}.",
                        message.GetType().Name);

                    break;
            }
        }
    }

    private async Task ProcessInMemoryEventAsync(
        InMemoryEventBus.InMemoryPublishedEvent publishedEvent,
        CancellationToken cancellationToken)
    {
        if (publishedEvent.Message
            is not OrderCreated orderCreated)
        {
            logger.LogWarning(
                "Unsupported in-memory event type {EventType}.",
                publishedEvent.Message.GetType().Name);

            return;
        }

        var parentContext =
            default(ActivityContext);

        if (!string.IsNullOrWhiteSpace(
                publishedEvent.TraceParent))
        {
            ActivityContext.TryParse(
                publishedEvent.TraceParent,
                publishedEvent.TraceState,
                out parentContext);
        }

        using var activity =
            EuroTradeActivitySource.Source.StartActivity(
                "ProcessOrderCreated",
                ActivityKind.Consumer,
                parentContext);

        activity?.SetTag(
            "messaging.system",
            "in_memory");

        activity?.SetTag(
            "messaging.message.id",
            publishedEvent.MessageId);

        activity?.SetTag(
            "order.id",
            orderCreated.OrderId);

        activity?.SetTag(
            "order.tenant_id",
            orderCreated.TenantId);

        logger.LogInformation(
            "OrderCreated event consumed in memory. " +
            "OrderId: {OrderId}, TenantId: {TenantId}, " +
            "MessageId: {MessageId}",
            orderCreated.OrderId,
            orderCreated.TenantId,
            publishedEvent.MessageId);

        if (string.IsNullOrWhiteSpace(
                publishedEvent.MessageId))
        {
            logger.LogWarning(
                "In-memory OrderCreated event {OrderId} " +
                "does not contain a message ID.",
                orderCreated.OrderId);

            activity?.SetStatus(
                ActivityStatusCode.Error,
                "Missing message ID.");

            return;
        }

        try
        {
            await RecordInboxMessageAsync(
                publishedEvent.MessageId,
                cancellationToken);

            activity?.SetStatus(
                ActivityStatusCode.Ok);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            logger.LogError(
                exception,
                "Error processing in-memory message " +
                "{MessageId}.",
                publishedEvent.MessageId);

            throw;
        }
    }

    private async Task RecordInboxMessageAsync(
       string messageId,
       CancellationToken cancellationToken)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        var recorded =
            await inboxMessageStore.TryRecordAsync(
                dbContext,
                messageId,
                cancellationToken);

        if (!recorded)
        {
            logger.LogInformation(
                "Inbox message {MessageId} " +
                "was already processed.",
                messageId);
        }
    }
}