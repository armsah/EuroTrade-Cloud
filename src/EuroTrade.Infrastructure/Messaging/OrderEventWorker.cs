using Azure.Messaging.ServiceBus;
using EuroTrade.Application.Messaging;
using EuroTrade.Application.Orders.Events;
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
                case AzureServiceBusEventConsumer.ServiceBusConsumedEvent
                    consumedEvent:

                    await ProcessServiceBusEventAsync(
                        consumedEvent,
                        forceFailure,
                        stoppingToken);

                    break;

                case OrderCreated orderCreated:

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

    private async Task ProcessServiceBusEventAsync(
        AzureServiceBusEventConsumer.ServiceBusConsumedEvent consumedEvent,
        bool forceFailure,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (consumedEvent.Event)
            {
                case OrderCreated orderCreated:

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

                    break;

                default:

                    logger.LogWarning(
                        "Unsupported event type {EventType}.",
                        consumedEvent.Event.GetType().Name);

                    break;
            }
        }
        catch (Exception exception)
        {
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