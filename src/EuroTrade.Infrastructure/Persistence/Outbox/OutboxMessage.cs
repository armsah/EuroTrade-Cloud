namespace EuroTrade.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public string MessageType { get; set; } = null!;

    public string Payload { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? Error { get; set; }

    // W3C distributed-tracing context captured when the
    // business operation creates the outbox message.
    public string? TraceParent { get; set; }

    public string? TraceState { get; set; }
}