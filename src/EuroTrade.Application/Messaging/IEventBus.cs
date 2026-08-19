namespace EuroTrade.Application.Messaging;

public interface IEventBus
{
    Task PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default);
}