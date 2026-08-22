using System.Security.Claims;
using System.Text.Encodings.Web;

using EuroTrade.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EuroTrade.E2E.Tests;

public sealed class EuroTradeApiFactory
    : WebApplicationFactory<Program>
{
    private readonly string _databasePath;

    public EuroTradeApiFactory()
    {
        // Every WebApplicationFactory gets its own SQLite database.
        // This prevents parallel E2E test hosts from sharing the
        // same database file and racing during EnsureCreated().
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"EuroTradeE2E_{Guid.NewGuid():N}.db");
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "Sqlite",

                    ["ConnectionStrings:OrdersDb"] =
                        $"Data Source={_databasePath}",

                    ["ServiceBus:ConnectionString"] = null,

                    ["ServiceBus:QueueName"] = null
                });
        });

        builder.ConfigureServices(services =>
        {
            // Remove every production registration related to
            // the OrdersDbContext factory/options.
            services.RemoveAll<
                IDbContextFactory<OrdersDbContext>>();

            services.RemoveAll<
                DbContextOptions<OrdersDbContext>>();

            services.RemoveAll<
                IDbContextOptionsConfiguration<OrdersDbContext>>();

            // Register a completely isolated SQLite database for
            // this WebApplicationFactory instance.
            services.AddDbContextFactory<OrdersDbContext>(
                options =>
                {
                    options.UseSqlite(
                        $"Data Source={_databasePath}");
                });

            // Replace production JWT authentication with a
            // deterministic authentication scheme for E2E tests.
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    TestAuthenticationHandler.SchemeName;

                options.DefaultChallengeScheme =
                    TestAuthenticationHandler.SchemeName;
            })
            .AddScheme<
                AuthenticationSchemeOptions,
                TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                _ =>
                {
                });
        });
    }

    protected override IHost CreateHost(
        IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope =
            host.Services.CreateScope();

        var dbContextFactory =
            scope.ServiceProvider
                .GetRequiredService<
                    IDbContextFactory<OrdersDbContext>>();

        using var dbContext =
            dbContextFactory.CreateDbContext();

        // E2E tests use SQLite and therefore must not attempt
        // to execute the PostgreSQL migration pipeline.
        //
        // EnsureCreated() creates the SQLite schema for this
        // factory's private database.
        dbContext.Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                if (File.Exists(_databasePath))
                {
                    File.Delete(_databasePath);
                }
            }
            catch
            {
                // Do not mask a test failure because Windows may
                // still have a SQLite file handle open during
                // WebApplicationFactory disposal.
            }
        }

        base.Dispose(disposing);
    }
}

public sealed class TestAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                "test-user"),

            new Claim(
                ClaimTypes.Name,
                "test-user"),

            new Claim(
                "tid",
                Guid.Empty.ToString())
        };

        var identity = new ClaimsIdentity(
            claims,
            SchemeName);

        var principal =
            new ClaimsPrincipal(identity);

        var ticket =
            new AuthenticationTicket(
                principal,
                SchemeName);

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }
}