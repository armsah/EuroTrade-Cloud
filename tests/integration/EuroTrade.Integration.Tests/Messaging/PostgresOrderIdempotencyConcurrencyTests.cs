using EuroTrade.Application.Orders;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Domain.Orders;
using EuroTrade.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace EuroTrade.Integration.Tests.Messaging;

[Collection("Postgres integration")]
public sealed class PostgresOrderIdempotencyConcurrencyTests(
    PostgresTestFixture postgres)
{

    [Fact]
    public async Task Concurrent_same_tenant_same_key_creates_one_order()
    {
        var connectionString =
            postgres.ConnectionString;

        var options =
            new DbContextOptionsBuilder<
                OrdersDbContext>()
                .UseNpgsql(
                    connectionString)
                .Options;

        await ResetDatabaseAsync(
            options);

        try
        {
            var tenantId =
                Guid.NewGuid();

            var customerId =
                Guid.NewGuid();

            var productId =
                Guid.NewGuid();

            const int quantity =
                5;

            const string idempotencyKey =
                "concurrent-order-key";

            var firstOrder =
                Order.Create(
                    tenantId,
                    customerId,
                    productId,
                    quantity);

            var secondOrder =
                Order.Create(
                    tenantId,
                    customerId,
                    productId,
                    quantity);

            var firstEvent =
                CreateOrderCreated(
                    firstOrder);

            var secondEvent =
                CreateOrderCreated(
                    secondOrder);

            await using var contextA =
                new OrdersDbContext(
                    options);

            await using var contextB =
                new OrdersDbContext(
                    options);

            var writerA =
                new EfOrderWriter(
                    contextA);

            var writerB =
                new EfOrderWriter(
                    contextB);

            var taskA =
                writerA.AddAsync(
                    firstOrder,
                    firstEvent,
                    idempotencyKey);

            var taskB =
                writerB.AddAsync(
                    secondOrder,
                    secondEvent,
                    idempotencyKey);

            var results =
                await Task.WhenAll(
                    taskA,
                    taskB);

            Assert.Equal(
                1,
                results.Count(
                    result =>
                        result.WasCreated));

            Assert.Equal(
                1,
                results.Count(
                    result =>
                        !result.WasCreated));

            Assert.Equal(
                results[0].Order.Id,
                results[1].Order.Id);

            await using var verificationContext =
                new OrdersDbContext(
                    options);

            var orderCount =
                await verificationContext.Orders
                    .CountAsync(
                        order =>
                            order.TenantId ==
                            tenantId);

            Assert.Equal(
                1,
                orderCount);

            var idempotencyCount =
                await verificationContext
                    .IdempotencyRecords
                    .CountAsync(
                        record =>
                            record.TenantId ==
                                tenantId &&
                            record.IdempotencyKey ==
                                idempotencyKey);

            Assert.Equal(
                1,
                idempotencyCount);

            var outboxCount =
                await verificationContext
                    .OutboxMessages
                    .CountAsync();

            Assert.Equal(
                1,
                outboxCount);
        }
        finally
        {
            await ResetDatabaseAsync(
                options);
        }
    }

    private static OrderCreated CreateOrderCreated(
        Order order)
    {
        return new OrderCreated(
            order.Id,
            order.TenantId,
            order.CustomerId,
            order.ProductId,
            order.Quantity,
            order.CreatedAt);
    }

    private static async Task ResetDatabaseAsync(
        DbContextOptions<OrdersDbContext> options)
    {
        await using var dbContext =
            new OrdersDbContext(
                options);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DROP SCHEMA IF EXISTS public CASCADE;
            CREATE SCHEMA public;
            """);

        await dbContext.Database
            .EnsureCreatedAsync();
    }
}