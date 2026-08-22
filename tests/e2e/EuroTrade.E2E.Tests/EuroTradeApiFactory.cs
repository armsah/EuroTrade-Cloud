using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using EuroTrade.Infrastructure.Persistence;

namespace EuroTrade.E2E.Tests;

public sealed class EuroTradeApiFactory : WebApplicationFactory<Program>
{
    private const string TestAuthenticationScheme = "Test";

    private const string TestDatabaseName =
        "EuroTradeE2E";

    private readonly SqliteConnection _connection;

    private readonly SemaphoreSlim _databaseLock =
        new(1, 1);

    public EuroTradeApiFactory()
    {
        // Shared in-memory SQLite database.
        //
        // The connection remains open for the lifetime of this factory.
        // All EF Core connections using the same URI will therefore use
        // the same in-memory database.
        _connection =
            new SqliteConnection(
                $"Data Source=file:{TestDatabaseName};" +
                "Mode=Memory;" +
                "Cache=Shared;");

        _connection.Open();
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Tell the existing production infrastructure registration
        // to use SQLite instead of PostgreSQL.
        //
        // DependencyInjection.cs already supports:
        //
        // Database:Provider = Sqlite
        //
        // so we do not need to remove/re-register EF services here.
        builder.UseSetting(
            "Database:Provider",
            "Sqlite");

        builder.UseSetting(
            "ConnectionStrings:OrdersDb",
            $"Data Source=file:{TestDatabaseName};" +
            "Mode=Memory;" +
            "Cache=Shared;");

        builder.ConfigureServices(services =>
        {
            // ------------------------------------------------------------
            // Test authentication.
            //
            // The production API protects the order endpoints with
            // RequireAuthorization(). E2E tests should not require a
            // real Microsoft Entra ID token.
            //
            // Instead, every test request receives a local authenticated
            // principal.
            // ------------------------------------------------------------

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme =
                        TestAuthenticationScheme;

                    options.DefaultChallengeScheme =
                        TestAuthenticationScheme;

                    options.DefaultScheme =
                        TestAuthenticationScheme;
                })
                .AddScheme<
                    AuthenticationSchemeOptions,
                    TestAuthenticationHandler>(
                    TestAuthenticationScheme,
                    _ =>
                    {
                    });

            // ------------------------------------------------------------
            // Create the SQLite database.
            //
            // AddInfrastructure() has already registered
            // IDbContextFactory<OrdersDbContext> using SQLite because
            // Database:Provider was set to Sqlite above.
            // ------------------------------------------------------------

            using var scope =
                services
                    .BuildServiceProvider()
                    .CreateScope();

            var factory =
                scope.ServiceProvider
                    .GetRequiredService<
                        IDbContextFactory<OrdersDbContext>>();

            using var db =
                factory.CreateDbContext();

            db.Database.EnsureCreated();
        });
    }

    public async Task ExecuteDbAsync(
        Func<OrdersDbContext, Task> operation)
    {
        await _databaseLock.WaitAsync();

        try
        {
            using var scope =
                Services.CreateScope();

            var factory =
                scope.ServiceProvider
                    .GetRequiredService<
                        IDbContextFactory<OrdersDbContext>>();

            await using var db =
                await factory.CreateDbContextAsync();

            await operation(db);
        }
        finally
        {
            _databaseLock.Release();
        }
    }

    public async Task<T> ExecuteDbAsync<T>(
        Func<OrdersDbContext, Task<T>> operation)
    {
        await _databaseLock.WaitAsync();

        try
        {
            using var scope =
                Services.CreateScope();

            var factory =
                scope.ServiceProvider
                    .GetRequiredService<
                        IDbContextFactory<OrdersDbContext>>();

            await using var db =
                await factory.CreateDbContextAsync();

            return await operation(db);
        }
        finally
        {
            _databaseLock.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _databaseLock.Dispose();
            _connection.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class TestAuthenticationHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
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
            var claims =
                new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        "e2e-test-user"),

                    new Claim(
                        ClaimTypes.Name,
                        "E2E Test User")
                };

            var identity =
                new ClaimsIdentity(
                    claims,
                    TestAuthenticationScheme);

            var principal =
                new ClaimsPrincipal(identity);

            var ticket =
                new AuthenticationTicket(
                    principal,
                    TestAuthenticationScheme);

            return Task.FromResult(
                AuthenticateResult.Success(ticket));
        }
    }
}