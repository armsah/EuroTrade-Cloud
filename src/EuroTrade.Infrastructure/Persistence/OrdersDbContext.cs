using EuroTrade.Domain.Orders;
using EuroTrade.Infrastructure.Persistence.Inbox;
using EuroTrade.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace EuroTrade.Infrastructure.Persistence;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

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

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");

            entity.HasKey(message => message.Id);

            entity.Property(message => message.MessageType)
                .IsRequired();

            entity.Property(message => message.Payload)
                .IsRequired();

            entity.Property(message => message.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("inbox_messages");

            entity.HasKey(message => message.Id);

            entity.Property(message => message.MessageId)
                .IsRequired();

            entity.HasIndex(message => message.MessageId)
                .IsUnique();

            entity.Property(message => message.ReceivedAt)
                .IsRequired();
        });
    }
}
