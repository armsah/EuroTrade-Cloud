using System.Threading.Channels;
using EuroTrade.Application.Messaging;

namespace EuroTrade.Infrastructure.Messaging;

public sealed class InMemoryEventBus : IEventBus, IEventConsumer
{
    private readonly Channel<object> _channel =
        Channel.CreateUnbounded<object>(
            new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            });

    public async Task PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await _channel.Writer.WriteAsync(
            message,
            cancellationToken);
    }

    public IAsyncEnumerable<object> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(
            cancellationToken);
    }
}