using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EuroTrade.Infrastructure.Persistence;

public sealed class OrdersDbContextFactory
    : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__OrdersDb")
            ?? "Host=localhost;Port=5432;Database=eurotrade;Username=eurotrade;Password=eurotrade";

        var optionsBuilder =
            new DbContextOptionsBuilder<OrdersDbContext>();

        optionsBuilder.UseNpgsql(connectionString);

        return new OrdersDbContext(optionsBuilder.Options);
    }
}