using EuroTrade.Application.Messaging;
using EuroTrade.Application.Orders;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Domain.Orders;

namespace EuroTrade.Application.Tests.Orders;

public sealed class CreateOrderServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidCommand_ReturnsCreatedOrder()
    {
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var command = new CreateOrderCommand(
            tenantId,
            customerId,
            productId,
            3);

        var repository = new FakeOrderRepository();
        var eventBus = new FakeEventBus();

        var service = new CreateOrderService(
            repository,
            eventBus);

        var result = await service.ExecuteAsync(command);

        Assert.NotEqual(Guid.Empty, result.OrderId);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal(productId, result.ProductId);
        Assert.Equal(3, result.Quantity);
        Assert.Equal("Pending", result.Status);

        Assert.NotNull(repository.Order);
        Assert.Equal(result.OrderId, repository.Order.Id);

        Assert.NotNull(eventBus.PublishedEvent);
        Assert.Equal(result.OrderId, eventBus.PublishedEvent.OrderId);
        Assert.Equal(tenantId, eventBus.PublishedEvent.TenantId);
        Assert.Equal(customerId, eventBus.PublishedEvent.CustomerId);
        Assert.Equal(productId, eventBus.PublishedEvent.ProductId);
        Assert.Equal(3, eventBus.PublishedEvent.Quantity);
    }

    private sealed class FakeOrderRepository : IOrderRepository
    {
        public Order? Order { get; private set; }

        public Task AddAsync(
            Order order,
            CancellationToken cancellationToken = default)
        {
            Order = order;
            return Task.CompletedTask;
        }

        public Task<Order?> GetByIdAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Order?>(null);
        }
    }

    private sealed class FakeEventBus : IEventBus
    {
        public OrderCreated? PublishedEvent { get; private set; }

        public Task PublishAsync<T>(
            T message,
            CancellationToken cancellationToken = default)
        {
            if (message is not OrderCreated orderCreated)
            {
                throw new InvalidOperationException(
                    $"Unexpected event type: {typeof(T).Name}");
            }

            PublishedEvent = orderCreated;

            return Task.CompletedTask;
        }
    }
}