using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using EuroTrade.Application.Messaging;

namespace EuroTrade.Infrastructure.Messaging;

public sealed class InMemoryEventBus : IEventBus, IEventConsumer
{
    private readonly Channel<InMemoryPublishedEvent> _channel =
        Channel.CreateUnbounded<InMemoryPublishedEvent>(
            new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            });

    public async Task PublishAsync<T>(
        T message,
        string? messageId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var activity = Activity.Current;

        await _channel.Writer.WriteAsync(
            new InMemoryPublishedEvent(
                message,
                messageId,
                activity?.Id,
                activity?.TraceStateString),
            cancellationToken);
    }

    public IAsyncEnumerable<object> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        return ReadAllInternalAsync(cancellationToken);
    }

    private async IAsyncEnumerable<object> ReadAllInternalAsync(
        [EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        await foreach (
            var message in _channel.Reader.ReadAllAsync(
                cancellationToken))
        {
            yield return message;
        }
    }

    public sealed record InMemoryPublishedEvent(
        object Message,
        string? MessageId,
        string? TraceParent,
        string? TraceState);
}