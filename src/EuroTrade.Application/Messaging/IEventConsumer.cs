namespace EuroTrade.Application.Messaging;

public interface IEventConsumer
{
    IAsyncEnumerable<object> ReadAllAsync(
        CancellationToken cancellationToken = default);
}