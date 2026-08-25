using EuroTrade.Application.Orders;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Application.Tenancy;
using EuroTrade.Domain.Orders;

namespace EuroTrade.Application.Tests.Orders;

public sealed class CreateOrderCancellationTests
{
    [Fact]
    public async Task ExecuteAsync_propagates_cancellation_to_order_writer()
    {
        var tenantId =
            Guid.NewGuid();

        var writer =
            new CancellingOrderWriter();

        var tenantContext =
            new TestTenantContext(
                tenantId);

        var service =
            new CreateOrderService(
                writer,
                tenantContext);

        var command =
            new CreateOrderCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                "cancelled-order");

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<
            OperationCanceledException>(
            () =>
                service.ExecuteAsync(
                    command,
                    cancellationTokenSource.Token));

        Assert.True(
            writer.WasCalled);

        Assert.True(
            writer.ReceivedCancellationToken
                .IsCancellationRequested);
    }

    private sealed class CancellingOrderWriter
        : IOrderWriter
    {
        public bool WasCalled { get; private set; }

        public CancellationToken ReceivedCancellationToken
        {
            get;
            private set;
        }

        public Task<OrderWriteResult> AddAsync(
            Order order,
            OrderCreated orderCreated,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            WasCalled =
                true;

            ReceivedCancellationToken =
                cancellationToken;

            cancellationToken
                .ThrowIfCancellationRequested();

            throw new InvalidOperationException(
                "The cancellation token should have thrown.");
        }
    }

    private sealed class TestTenantContext(
        Guid tenantId)
        : ITenantContext
    {
        public Guid TenantId { get; } =
            tenantId;
    }
}