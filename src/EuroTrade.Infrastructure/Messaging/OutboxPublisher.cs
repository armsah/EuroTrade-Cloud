using System.Diagnostics;
using System.Text.Json;

using EuroTrade.Application.Messaging;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Application.Telemetry;

using EuroTrade.Infrastructure.Persistence;
using EuroTrade.Infrastructure.Persistence.Outbox;

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
        logger.LogInformation(
            "Outbox publisher started.");

        using var timer =
            new PeriodicTimer(TimeSpan.FromSeconds(2));

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

        var query =
            dbContext.OutboxMessages
                .Where(message =>
                    message.PublishedAt == null);

        List<OutboxMessage> messages;

        if (dbContext.Database.IsSqlite())
        {
            messages =
                (await query
                    .ToListAsync(cancellationToken))
                    .OrderBy(message => message.CreatedAt)
                    .Take(20)
                    .ToList();
        }
        else
        {
            messages =
                await query
                    .OrderBy(message => message.CreatedAt)
                    .Take(20)
                    .ToListAsync(cancellationToken);
        }

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
                                    $"Could not deserialize outbox " +
                                    $"message {message.Id}.");

                            var parentContext =
                                CreateParentContext(message);

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

                            await eventBus.PublishAsync(
                                orderCreated,
                                message.Id.ToString(),
                                cancellationToken);

                            message.PublishedAt =
                                DateTimeOffset.UtcNow;

                            message.Error = null;

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

                            break;
                        }

                    default:
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

    private static ActivityContext CreateParentContext(
        OutboxMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.TraceParent))
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