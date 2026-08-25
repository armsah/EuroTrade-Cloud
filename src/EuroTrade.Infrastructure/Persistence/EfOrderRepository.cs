using EuroTrade.Application.Orders;
using EuroTrade.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace EuroTrade.Infrastructure.Persistence;

public sealed class EfOrderRepository(OrdersDbContext dbContext)
    : IOrderRepository
{
    public async Task AddAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Orders.AddAsync(
            order,
            cancellationToken);

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<Order?> GetByTenantAndIdAsync(
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(
                order =>
                    order.TenantId == tenantId &&
                    order.Id == orderId,
                cancellationToken);
    }
}