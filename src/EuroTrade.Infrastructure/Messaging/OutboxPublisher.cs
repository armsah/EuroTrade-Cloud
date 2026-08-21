using System.Text.Json;
using EuroTrade.Application.Messaging;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EuroTrade.Infrastructure.Messaging;

public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxPublisher> logger)
    : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox publisher started.");

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(2));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingMessagesAsync(stoppingToken);
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
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Outbox publisher stopped.");
    }

    private async Task PublishPendingMessagesAsync(
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var eventBus =
            scope.ServiceProvider.GetRequiredService<IEventBus>();

        var messages = await dbContext.OutboxMessages
            .Where(message => message.PublishedAt == null)
            .OrderBy(message => message.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
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
                                $"Could not deserialize outbox message {message.Id}.");

                        await eventBus.PublishAsync(
                            orderCreated,
                            cancellationToken);

                        message.PublishedAt =
                            DateTimeOffset.UtcNow;

                        message.Error = null;

                        await dbContext.SaveChangesAsync(
                            cancellationToken);

                        logger.LogInformation(
                            "Published outbox message {MessageId}. " +
                            "MessageType: {MessageType}. " +
                            "OrderId: {OrderId}",
                            message.Id,
                            message.MessageType,
                            orderCreated.OrderId);

                        break;
                    }

                    default:
                        message.Error =
                            $"Unsupported outbox message type: {message.MessageType}";

                        await dbContext.SaveChangesAsync(
                            cancellationToken);

                        logger.LogError(
                            "Unsupported outbox message type {MessageType}. " +
                            "OutboxMessageId: {MessageId}",
                            message.MessageType,
                            message.Id);

                        break;
                }
            }
            catch (Exception exception)
            {
                message.Error = exception.Message;

                await dbContext.SaveChangesAsync(
                    cancellationToken);

                logger.LogError(
                    exception,
                    "Failed to publish outbox message {MessageId}. " +
                    "It will be retried.",
                    message.Id);
            }
        }
    }
}
