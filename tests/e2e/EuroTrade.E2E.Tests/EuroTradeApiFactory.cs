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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remove the production PostgreSQL DbContext registration.
            services.RemoveAll<OrdersDbContext>();
            services.RemoveAll<DbContextOptions<OrdersDbContext>>();

            // Remove the existing PostgreSQL EF provider registration.
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<OrdersDbContext>>();

            // Keep SQLite in-memory database alive for the lifetime
            // of the test server.
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<OrdersDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Create the SQLite schema.
            using var serviceProvider = services.BuildServiceProvider();

            using var scope = serviceProvider.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<OrdersDbContext>();

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
