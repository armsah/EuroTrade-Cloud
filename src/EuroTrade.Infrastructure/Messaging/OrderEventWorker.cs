using System.Diagnostics;

using Azure.Messaging.ServiceBus;

using EuroTrade.Application.Messaging;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Application.Telemetry;

using EuroTrade.Infrastructure.Persistence;
using EuroTrade.Infrastructure.Persistence.Inbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EuroTrade.Infrastructure.Messaging;

public sealed class OrderEventWorker(
    IEventConsumer eventConsumer,
    IDbContextFactory<OrdersDbContext> dbContextFactory,
    IConfiguration configuration,
    ILogger<OrderEventWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var forceFailure =
            configuration.GetValue<bool>(
                "ServiceBus:ForceProcessingFailure");

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

                case AzureServiceBusEventConsumer.ServiceBusConsumedEvent
                    consumedEvent:

                    await ProcessServiceBusEventAsync(
                        consumedEvent,
                        forceFailure,
                        stoppingToken);

                    break;

                case OrderCreated orderCreated:

                    // Backwards-compatible fallback for any direct
                    // in-memory OrderCreated messages that do not
                    // contain a message ID or trace context.
                    logger.LogInformation(
                        "OrderCreated event consumed in memory. " +
                        "OrderId: {OrderId}, TenantId: {TenantId}",
                        orderCreated.OrderId,
                        orderCreated.TenantId);

                    break;

                default:

                    logger.LogWarning(
                        "Unsupported event type {EventType}.",
                        message.GetType().Name);

                    break;
            }
        }
    }

    private async Task ProcessInMemoryEventAsync(
        InMemoryEventBus.InMemoryPublishedEvent publishedEvent,
        CancellationToken cancellationToken)
    {
        if (publishedEvent.Message is not OrderCreated orderCreated)
        {
            logger.LogWarning(
                "Unsupported in-memory event type {EventType}.",
                publishedEvent.Message.GetType().Name);

            return;
        }

        var parentContext = default(ActivityContext);

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
                "Error processing in-memory message {MessageId}.",
                publishedEvent.MessageId);

            throw;
        }
    }

    private async Task ProcessServiceBusEventAsync(
        AzureServiceBusEventConsumer.ServiceBusConsumedEvent consumedEvent,
        bool forceFailure,
        CancellationToken cancellationToken)
    {
        var parentContext = default(ActivityContext);

        if (consumedEvent.Message.ApplicationProperties.TryGetValue(
                "Diagnostic-Id",
                out var diagnosticIdValue))
        {
            var diagnosticId =
                diagnosticIdValue?.ToString();

            var traceState =
                consumedEvent.Message.ApplicationProperties.TryGetValue(
                    "TraceState",
                    out var traceStateValue)
                        ? traceStateValue?.ToString()
                        : null;

            if (!string.IsNullOrWhiteSpace(diagnosticId))
            {
                ActivityContext.TryParse(
                    diagnosticId,
                    traceState,
                    out parentContext);
            }
        }

        using var activity =
            EuroTradeActivitySource.Source.StartActivity(
                "ProcessOrderCreated",
                ActivityKind.Consumer,
                parentContext);

        activity?.SetTag(
            "messaging.system",
            "azure_service_bus");

        activity?.SetTag(
            "messaging.destination.name",
            consumedEvent.Message.Subject);

        activity?.SetTag(
            "messaging.message.id",
            consumedEvent.Message.MessageId);

        try
        {
            switch (consumedEvent.Event)
            {
                case OrderCreated orderCreated:

                    activity?.SetTag(
                        "order.id",
                        orderCreated.OrderId);

                    activity?.SetTag(
                        "order.tenant_id",
                        orderCreated.TenantId);

                    activity?.SetTag(
                        "order.customer_id",
                        orderCreated.CustomerId);

                    activity?.SetTag(
                        "order.product_id",
                        orderCreated.ProductId);

                    activity?.SetTag(
                        "order.quantity",
                        orderCreated.Quantity);

                    logger.LogInformation(
                        "OrderCreated event consumed. " +
                        "OrderId: {OrderId}, TenantId: {TenantId}, " +
                        "DeliveryCount: {DeliveryCount}",
                        orderCreated.OrderId,
                        orderCreated.TenantId,
                        consumedEvent.Message.DeliveryCount);

                    if (forceFailure)
                    {
                        throw new InvalidOperationException(
                            "Intentional P6 retry/DLQ test failure.");
                    }

                    await RecordInboxMessageAsync(
                        consumedEvent.Message.MessageId,
                        cancellationToken);

                    activity?.SetStatus(
                        ActivityStatusCode.Ok);

                    break;

                default:

                    activity?.SetStatus(
                        ActivityStatusCode.Error,
                        "Unsupported event type.");

                    logger.LogWarning(
                        "Unsupported event type {EventType}.",
                        consumedEvent.Event.GetType().Name);

                    break;
            }
        }
        catch (Exception exception)
        {
            activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            logger.LogError(
                exception,
                "Error processing Service Bus message {MessageId}. " +
                "DeliveryCount: {DeliveryCount}",
                consumedEvent.Message.MessageId,
                consumedEvent.Message.DeliveryCount);

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

        var exists =
            await dbContext.InboxMessages.AnyAsync(
                inbox =>
                    inbox.MessageId == messageId,
                cancellationToken);

        if (exists)
        {
            logger.LogInformation(
                "Inbox message {MessageId} was already processed.",
                messageId);

            return;
        }

        dbContext.InboxMessages.Add(
            new InboxMessage
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                ReceivedAt = DateTimeOffset.UtcNow,
                ProcessedAt = DateTimeOffset.UtcNow
            });

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }
}