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
    ServiceBusReceiver receiver,
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
            var message in eventConsumer.ReadAllAsync(stoppingToken))
        {
            if (message is not AzureServiceBusEventConsumer.ServiceBusConsumedEvent consumedEvent)
                continue;

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

                        await using (var dbContext =
                            await dbContextFactory.CreateDbContextAsync(
                                stoppingToken))
                        {
                            var exists =
                                await dbContext.InboxMessages.AnyAsync(
                                    inbox =>
                                        inbox.MessageId ==
                                        consumedEvent.Message.MessageId,
                                    stoppingToken);

                            if (!exists)
                            {
                                dbContext.InboxMessages.Add(
                                    new InboxMessage
                                    {
                                        Id = Guid.NewGuid(),
                                        MessageId =
                                            consumedEvent.Message.MessageId,
                                        ReceivedAt =
                                            DateTimeOffset.UtcNow,
                                        ProcessedAt =
                                            DateTimeOffset.UtcNow
                                    });

                                await dbContext.SaveChangesAsync(
                                    stoppingToken);
                            }
                        }

                        await receiver.CompleteMessageAsync(
                            consumedEvent.Message,
                            stoppingToken);

                        break;

                    default:

                        await receiver.DeadLetterMessageAsync(
                            consumedEvent.Message,
                            "UnknownEventType",
                            $"Unsupported event type: {consumedEvent.Event.GetType().Name}",
                            stoppingToken);

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

                await receiver.AbandonMessageAsync(
                    consumedEvent.Message,
                    cancellationToken: stoppingToken);
            }
        }
    }
}
