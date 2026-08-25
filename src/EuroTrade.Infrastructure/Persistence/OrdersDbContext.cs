using EuroTrade.Domain.Orders;
using EuroTrade.Infrastructure.Persistence.Idempotency;
using EuroTrade.Infrastructure.Persistence.Inbox;
using EuroTrade.Infrastructure.Persistence.Outbox;

using Microsoft.EntityFrameworkCore;

namespace EuroTrade.Infrastructure.Persistence;

public sealed class OrdersDbContext(
    DbContextOptions<OrdersDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders =>
        Set<Order>();

    public DbSet<OutboxMessage> OutboxMessages =>
        Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages =>
        Set<InboxMessage>();

    public DbSet<IdempotencyRecord> IdempotencyRecords =>
        Set<IdempotencyRecord>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
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

            entity.HasIndex(message => new
            {
                message.PublishedAt,
                message.CreatedAt
            });
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

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("idempotency_records");

            entity.HasKey(record => record.Id);

            entity.Property(record => record.Id)
                .ValueGeneratedNever();

            entity.Property(record => record.TenantId)
                .IsRequired();

            entity.Property(record => record.IdempotencyKey)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(record => record.OrderId)
                .IsRequired();

            entity.Property(record => record.CustomerId)
                .IsRequired();

            entity.Property(record => record.ProductId)
                .IsRequired();

            entity.Property(record => record.Quantity)
                .IsRequired();

            entity.Property(record => record.CreatedAt)
                .IsRequired();

            entity.HasIndex(record => new
            {
                record.TenantId,
                record.IdempotencyKey
            })
            .IsUnique();
        });
    }
}