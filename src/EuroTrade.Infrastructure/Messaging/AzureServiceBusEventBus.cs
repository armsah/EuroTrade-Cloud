using System.Text.Json;
using Azure.Messaging.ServiceBus;
using EuroTrade.Application.Messaging;
using EuroTrade.Application.Orders.Events;

namespace EuroTrade.Infrastructure.Messaging;

public sealed class AzureServiceBusEventBus(
    ServiceBusSender sender) : IEventBus
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var eventType = message switch
        {
            OrderCreated => nameof(OrderCreated),
            _ => throw new InvalidOperationException(
                $"Unsupported event type: {message.GetType().Name}")
        };

        var body = JsonSerializer.Serialize(
            message,
            JsonOptions);

        var serviceBusMessage = new ServiceBusMessage(body)
        {
            Subject = eventType,
            ContentType = "application/json"
        };

        serviceBusMessage.ApplicationProperties["eventType"] = eventType;

        await sender.SendMessageAsync(
            serviceBusMessage,
            cancellationToken);
    }
}