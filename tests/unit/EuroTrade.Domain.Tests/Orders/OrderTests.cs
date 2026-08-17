using EuroTrade.Domain.Orders;

namespace EuroTrade.Domain.Tests.Orders;

public sealed class OrderTests
{
    [Fact]
    public void Create_WithValidData_CreatesPendingOrder()
    {
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var order = Order.Create(
            tenantId,
            customerId,
            productId,
            5);

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(tenantId, order.TenantId);
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal(productId, order.ProductId);
        Assert.Equal(5, order.Quantity);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public void Create_WithZeroQuantity_Throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Order.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                0));

        Assert.Equal("quantity", exception.ParamName);
    }

    [Fact]
    public void Create_WithMissingTenant_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Order.Create(
                Guid.Empty,
                Guid.NewGuid(),
                Guid.NewGuid(),
                1));
    }
}