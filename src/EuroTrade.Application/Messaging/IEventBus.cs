namespace EuroTrade.Application.Messaging;

public interface IEventBus
{
    Task PublishAsync<T>(
        T message,
        string? messageId = null,
        CancellationToken cancellationToken = default);
}

