using EuroTrade.Application.Orders;

namespace EuroTrade.Application.Tests.Orders;

public sealed class CreateOrderServiceTests
{
    [Fact]
    public void Execute_WithValidCommand_ReturnsCreatedOrder()
    {
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var command = new CreateOrderCommand(
            tenantId,
            customerId,
            productId,
            3);

        var service = new CreateOrderService();

        var result = service.Execute(command);

        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal(productId, result.ProductId);
        Assert.Equal(3, result.Quantity);
        Assert.Equal("Pending", result.Status);
    }
}