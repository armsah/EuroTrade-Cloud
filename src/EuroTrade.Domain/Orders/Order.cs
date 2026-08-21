namespace EuroTrade.Domain.Orders;

public sealed class Order
{
    public Guid Id { get; }
    public Guid TenantId { get; }
    public Guid CustomerId { get; }
    public Guid ProductId { get; }
    public int Quantity { get; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    private Order(
        Guid id,
        Guid tenantId,
        Guid customerId,
        Guid productId,
        int quantity,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        ProductId = productId;
        Quantity = quantity;
        Status = OrderStatus.Pending;
        CreatedAt = createdAt;
    }

    public static Order Create(
        Guid tenantId,
        Guid customerId,
        Guid productId,
        int quantity,
        DateTimeOffset? createdAt = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

        if (customerId == Guid.Empty)
            throw new ArgumentException("Customer ID is required.", nameof(customerId));

        if (productId == Guid.Empty)
            throw new ArgumentException("Product ID is required.", nameof(productId));

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity must be greater than zero.");

        return new Order(
            Guid.NewGuid(),
            tenantId,
            customerId,
            productId,
            quantity,
            createdAt ?? DateTimeOffset.UtcNow);
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            return;

        Status = OrderStatus.Confirmed;
    }
}
