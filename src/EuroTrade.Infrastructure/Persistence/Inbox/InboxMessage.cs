namespace EuroTrade.Infrastructure.Persistence.Inbox;

public sealed class InboxMessage
{
    public Guid Id { get; set; }

    public string MessageId { get; set; } = null!;

    public DateTimeOffset ReceivedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}
