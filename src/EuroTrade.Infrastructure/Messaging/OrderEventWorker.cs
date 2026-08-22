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
                    // have a message ID.
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

            return;
        }

        await RecordInboxMessageAsync(
            publishedEvent.MessageId,
            cancellationToken);
    }

    private async Task ProcessServiceBusEventAsync(
        AzureServiceBusEventConsumer.ServiceBusConsumedEvent consumedEvent,
        bool forceFailure,
        CancellationToken cancellationToken)
    {
        var parentContext = default(ActivityContext);

        if (consumedEvent.Message.ApplicationProperties.TryGetValue(
                "Diagnostic-Id",
                out var diagnosticIdValue)
            && diagnosticIdValue is string diagnosticId)
        {
            ActivityContext.TryParse(
                diagnosticId,
                null,
                out parentContext);
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
                inbox => inbox.MessageId == messageId,
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