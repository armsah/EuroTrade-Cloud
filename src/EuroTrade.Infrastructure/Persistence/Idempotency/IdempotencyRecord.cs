namespace EuroTrade.Infrastructure.Persistence.Idempotency;

public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string IdempotencyKey { get; set; } =
        string.Empty;

    public Guid OrderId { get; set; }

    public Guid CustomerId { get; set; }

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}