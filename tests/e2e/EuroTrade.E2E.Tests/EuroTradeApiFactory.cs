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

public sealed class EuroTradeApiFactory
    : WebApplicationFactory<Program>
{
    private const string TestAuthenticationScheme = "Test";

    public const string TestTenantHeader =
        "X-Test-Tenant-Id";

    private const string TestDatabaseName =
        "EuroTradeE2E";

    public static readonly Guid DefaultTenantId =
        Guid.Parse(
            "11111111-1111-1111-1111-111111111111");

    private readonly SqliteConnection _connection;

    private readonly SemaphoreSlim _databaseLock =
        new(1, 1);

    public EuroTradeApiFactory()
    {
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
        });
    }

    public HttpClient CreateClientForTenant(
        Guid tenantId)
    {
        var client = CreateClient();

        client.DefaultRequestHeaders.Add(
            TestTenantHeader,
            tenantId.ToString());

        return client;
    }

    public async Task InitializeDatabaseAsync()
    {
        await ExecuteDbAsync(
            async db =>
            {
                await db.Database.EnsureCreatedAsync();
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

            await db.Database.EnsureCreatedAsync();

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

            await db.Database.EnsureCreatedAsync();

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
            var tenantId =
                DefaultTenantId;

            if (Request.Headers.TryGetValue(
                    TestTenantHeader,
                    out var tenantHeader) &&
                Guid.TryParse(
                    tenantHeader.ToString(),
                    out var requestedTenantId))
            {
                tenantId =
                    requestedTenantId;
            }

            var claims =
                new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        $"e2e-user-{tenantId}"),

                    new Claim(
                        ClaimTypes.Name,
                        "E2E Test User"),

                    new Claim(
                        "tenant_id",
                        tenantId.ToString())
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