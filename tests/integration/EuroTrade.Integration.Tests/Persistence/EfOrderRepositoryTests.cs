using EuroTrade.Domain.Orders;
using EuroTrade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EuroTrade.Integration.Tests.Persistence;

public sealed class EfOrderRepositoryTests
{
    [Fact]
    public async Task AddAsync_PersistsOrderToDatabase()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(
                "appsettings.Development.json",
                optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("OrdersDb")
            ?? throw new InvalidOperationException(
                "Connection string 'OrdersDb' was not configured.");

        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var dbContext = new OrdersDbContext(options);

        var repository = new EfOrderRepository(dbContext);

        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var order = Order.Create(
            tenantId,
            customerId,
            productId,
            7);

        await repository.AddAsync(order);

        var persistedOrder = await dbContext.Orders
            .AsNoTracking()
            .SingleAsync(x => x.Id == order.Id);

        Assert.Equal(order.Id, persistedOrder.Id);
        Assert.Equal(tenantId, persistedOrder.TenantId);
        Assert.Equal(customerId, persistedOrder.CustomerId);
        Assert.Equal(productId, persistedOrder.ProductId);
        Assert.Equal(7, persistedOrder.Quantity);
        Assert.Equal(OrderStatus.Pending, persistedOrder.Status);
    }
}