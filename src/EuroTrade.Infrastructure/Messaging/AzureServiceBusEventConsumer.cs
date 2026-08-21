using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using EuroTrade.Application.Messaging;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Infrastructure.Persistence;
using EuroTrade.Infrastructure.Persistence.Inbox;
using Microsoft.EntityFrameworkCore;

namespace EuroTrade.Infrastructure.Messaging;

public sealed class AzureServiceBusEventConsumer(
    ServiceBusReceiver receiver,
    IDbContextFactory<OrdersDbContext> dbContextFactory) : IEventConsumer
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async IAsyncEnumerable<object> ReadAllAsync(
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ServiceBusReceivedMessage? message = null;

            try
            {
                message = await receiver.ReceiveMessageAsync(
                    TimeSpan.FromSeconds(5),
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            if (message is null)
                continue;

            await using var dbContext =
                await dbContextFactory.CreateDbContextAsync(
                    cancellationToken);

            var alreadyReceived =
                await dbContext.InboxMessages.AnyAsync(
                    inbox => inbox.MessageId == message.MessageId,
                    cancellationToken);

            if (alreadyReceived)
            {
                await receiver.CompleteMessageAsync(
                    message,
                    cancellationToken);

                continue;
            }

            object? domainEvent;

            try
            {
                var eventType =
                    message.ApplicationProperties.TryGetValue(
                        "eventType",
                        out var eventTypeValue)
                            ? eventTypeValue?.ToString()
                            : message.Subject;

                domainEvent = eventType switch
                {
                    nameof(OrderCreated) =>
                        JsonSerializer.Deserialize<OrderCreated>(
                            message.Body.ToString(),
                            JsonOptions),

                    _ => null
                };
            }
            catch (JsonException exception)
            {
                await receiver.DeadLetterMessageAsync(
                    message,
                    "InvalidEventPayload",
                    exception.Message,
                    cancellationToken);

                continue;
            }

            if (domainEvent is null)
            {
                await receiver.DeadLetterMessageAsync(
                    message,
                    "UnknownEventType",
                    "Unsupported or missing event type.",
                    cancellationToken);

                continue;
            }

            yield return new ServiceBusConsumedEvent(
                message,
                domainEvent);
        }
    }

    public sealed record ServiceBusConsumedEvent(
        ServiceBusReceivedMessage Message,
        object Event);
}
