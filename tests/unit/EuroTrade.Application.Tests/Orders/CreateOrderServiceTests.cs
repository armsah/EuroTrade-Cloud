using EuroTrade.Application.Orders;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Application.Tenancy;
using EuroTrade.Domain.Orders;

namespace EuroTrade.Application.Tests.Orders;

public sealed class CreateOrderServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidCommand_UsesAuthorizedTenant()
    {
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var command = new CreateOrderCommand(
            customerId,
            productId,
            3);

        var writer = new FakeOrderWriter();
        var tenantContext =
            new FakeTenantContext(tenantId);

        var service =
            new CreateOrderService(
                writer,
                tenantContext);

        var result =
            await service.ExecuteAsync(command);

        Assert.NotEqual(
            Guid.Empty,
            result.OrderId);

        Assert.Equal(
            tenantId,
            result.TenantId);

        Assert.Equal(
            customerId,
            result.CustomerId);

        Assert.Equal(
            productId,
            result.ProductId);

        Assert.Equal(
            3,
            result.Quantity);

        Assert.Equal(
            "Pending",
            result.Status);

        Assert.NotNull(writer.Order);

        Assert.Equal(
            tenantId,
            writer.Order!.TenantId);

        Assert.Equal(
            result.OrderId,
            writer.Order.Id);

        Assert.NotNull(writer.OrderCreated);

        Assert.Equal(
            result.OrderId,
            writer.OrderCreated!.OrderId);

        Assert.Equal(
            tenantId,
            writer.OrderCreated.TenantId);

        Assert.Equal(
            customerId,
            writer.OrderCreated.CustomerId);

        Assert.Equal(
            productId,
            writer.OrderCreated.ProductId);

        Assert.Equal(
            3,
            writer.OrderCreated.Quantity);
    }

    private sealed class FakeTenantContext(
        Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } =
            tenantId;
    }

    private sealed class FakeOrderWriter
        : IOrderWriter
    {
        public Order? Order { get; private set; }

        public OrderCreated? OrderCreated
        {
            get;
            private set;
        }

        public Task AddAsync(
            Order order,
            OrderCreated orderCreated,
            CancellationToken cancellationToken = default)
        {
            Order = order;
            OrderCreated = orderCreated;

            return Task.CompletedTask;
        }
    }
}