using EuroTrade.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EuroTrade.E2E.Tests;

public sealed class EuroTradeApiFactory
    : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextFactory<OrdersDbContext>>();
            services.RemoveAll<OrdersDbContext>();
            services.RemoveAll<DbContextOptions<OrdersDbContext>>();
            services.RemoveAll<
                Microsoft.EntityFrameworkCore.Infrastructure
                    .IDbContextOptionsConfiguration<OrdersDbContext>>();

            _connection = new SqliteConnection(
                "DataSource=:memory:");

            _connection.Open();

            services.AddDbContextFactory<OrdersDbContext>(
                options =>
                {
                    options.UseSqlite(_connection);
                });

            using var serviceProvider =
                services.BuildServiceProvider();

            using var scope =
                serviceProvider.CreateScope();

            var dbFactory =
                scope.ServiceProvider
                    .GetRequiredService<
                        IDbContextFactory<OrdersDbContext>>();

            using var db =
                dbFactory.CreateDbContext();

            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Dispose();
        }

        base.Dispose(disposing);
    }
}