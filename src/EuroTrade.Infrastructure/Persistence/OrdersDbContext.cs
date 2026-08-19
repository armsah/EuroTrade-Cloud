using EuroTrade.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace EuroTrade.Infrastructure.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");

            entity.HasKey(order => order.Id);

            entity.Property(order => order.Id)
                .ValueGeneratedNever();

            entity.Property(order => order.TenantId)
                .IsRequired();

            entity.Property(order => order.CustomerId)
                .IsRequired();

            entity.Property(order => order.ProductId)
                .IsRequired();

            entity.Property(order => order.Quantity)
                .IsRequired();

            entity.Property(order => order.Status)
                .HasConversion<string>()
                .IsRequired();

            entity.Property(order => order.CreatedAt)
                .IsRequired();
        });
    }
}
