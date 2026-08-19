using EuroTrade.Application.Messaging;
using EuroTrade.Application.Orders.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EuroTrade.Infrastructure.Messaging;

public sealed class OrderEventWorker(
    IEventConsumer eventConsumer,
    ILogger<OrderEventWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await foreach (var message in eventConsumer.ReadAllAsync(stoppingToken))
        {
            switch (message)
            {
                case OrderCreated orderCreated:
                    logger.LogInformation(
                        "OrderCreated event consumed. OrderId: {OrderId}, TenantId: {TenantId}",
                        orderCreated.OrderId,
                        orderCreated.TenantId);
                    break;

                default:
                    logger.LogWarning(
                        "Unknown event type received: {EventType}",
                        message.GetType().Name);
                    break;
            }
        }
    }
}