using System.ComponentModel.DataAnnotations.Schema;

namespace EuroTrade.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public string MessageType { get; set; } =
        null!;

    public string Payload { get; set; } =
        null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset? FailedAt { get; set; }

    // Compatibility alias for existing tests/code.
    // LastError remains mapped to the existing database
    // column named "Error".
    [NotMapped]
    public string? Error
    {
        get => LastError;
        set => LastError = value;
    }

    // W3C distributed-tracing context captured when the
    // business operation creates the outbox message.
    public string? TraceParent { get; set; }

    public string? TraceState { get; set; }
}