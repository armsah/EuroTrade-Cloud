using EuroTrade.Application.Orders.Events;
using EuroTrade.Domain.Orders;
using EuroTrade.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace EuroTrade.Integration.Tests.Messaging;

[Collection("Postgres integration")]
public sealed class PostgresOutboxTransactionRollbackTests
{
    private const string ConnectionStringEnvironmentVariable =
        "EUROTRADE_TEST_POSTGRES";

    [Fact]
    public async Task AddAsync_WhenIdempotencyInsertFails_RollsBackEntireTransaction()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                ConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(
                connectionString))
        {
            return;
        }

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

            var order =
                Order.Create(
                    tenantId,
                    customerId,
                    productId,
                    5);

            var orderCreated =
                new OrderCreated(
                    order.Id,
                    order.TenantId,
                    order.CustomerId,
                    order.ProductId,
                    order.Quantity,
                    order.CreatedAt);

            // The database column is varchar(200).
            // This deliberately causes PostgreSQL to reject
            // the IdempotencyRecord during SaveChangesAsync().
            var invalidIdempotencyKey =
                new string(
                    'x',
                    201);

            await using var context =
                new OrdersDbContext(
                    options);

            var writer =
                new EfOrderWriter(
                    context);

            await Assert.ThrowsAsync<
                DbUpdateException>(
                () =>
                    writer.AddAsync(
                        order,
                        orderCreated,
                        invalidIdempotencyKey));

            await using var verificationContext =
                new OrdersDbContext(
                    options);

            var orderExists =
                await verificationContext.Orders
                    .AnyAsync(
                        candidate =>
                            candidate.Id ==
                            order.Id);

            var outboxCount =
                await verificationContext
                    .OutboxMessages
                    .CountAsync();

            var idempotencyCount =
                await verificationContext
                    .IdempotencyRecords
                    .CountAsync(
                        record =>
                            record.TenantId ==
                            tenantId);

            Assert.False(
                orderExists);

            Assert.Equal(
                0,
                outboxCount);

            Assert.Equal(
                0,
                idempotencyCount);
        }
        finally
        {
            await ResetDatabaseAsync(
                options);
        }
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