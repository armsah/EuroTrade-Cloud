using System.Security.Claims;
using System.Text.Encodings.Web;

using EuroTrade.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EuroTrade.E2E.Tests;

public sealed class EuroTradeApiFactory
    : WebApplicationFactory<Program>
{
    private const string TestAuthenticationScheme =
        "Test";

    public const string TestTenantHeader =
        "X-Test-Tenant-Id";

    public const string TestScopesHeader =
        "X-Test-Scopes";

    public const string TestUnauthenticatedHeader =
        "X-Test-Unauthenticated";

    public const string TestTenantClaimHeader =
        "X-Test-Tenant-Claim";

    public const string TestOmitTenantClaimHeader =
        "X-Test-Omit-Tenant-Claim";

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

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var options =
            new DbContextOptionsBuilder<OrdersDbContext>()
                .UseSqlite(
                    _connection)
                .Options;

        using var db =
            new OrdersDbContext(
                options);

        db.Database.EnsureCreated();
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment(
            "Testing");

        builder.UseSetting(
            "Database:Provider",
            "Sqlite");

        builder.UseSetting(
            "ConnectionStrings:OrdersDb",
            $"Data Source=file:{TestDatabaseName};" +
            "Mode=Memory;" +
            "Cache=Shared;");

        builder.ConfigureServices(
            services =>
            {
                services
                    .AddAuthentication(
                        options =>
                        {
                            options.DefaultAuthenticateScheme =
                                TestAuthenticationScheme;

                            options.DefaultChallengeScheme =
                                TestAuthenticationScheme;

                            options.DefaultForbidScheme =
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
        var client =
            CreateClient();

        client.DefaultRequestHeaders.Add(
            TestTenantHeader,
            tenantId.ToString());

        return client;
    }

    public HttpClient CreateClientWithScopes(
        params string[] scopes)
    {
        var client =
            CreateClient();

        client.DefaultRequestHeaders.Add(
            TestScopesHeader,
            string.Join(
                ' ',
                scopes));

        return client;
    }

    public HttpClient CreateClientForTenantWithScopes(
        Guid tenantId,
        params string[] scopes)
    {
        var client =
            CreateClient();

        client.DefaultRequestHeaders.Add(
            TestTenantHeader,
            tenantId.ToString());

        client.DefaultRequestHeaders.Add(
            TestScopesHeader,
            string.Join(
                ' ',
                scopes));

        return client;
    }

    public HttpClient CreateUnauthenticatedClient()
    {
        var client =
            CreateClient();

        client.DefaultRequestHeaders.Add(
            TestUnauthenticatedHeader,
            "true");

        return client;
    }

    public HttpClient CreateClientWithRawTenantClaim(
        string tenantClaim,
        params string[] scopes)
    {
        var client =
            CreateClient();

        client.DefaultRequestHeaders.Add(
            TestTenantClaimHeader,
            tenantClaim);

        client.DefaultRequestHeaders.Add(
            TestScopesHeader,
            string.Join(
                ' ',
                scopes));

        return client;
    }

    public HttpClient CreateClientWithoutTenantClaim(
        params string[] scopes)
    {
        var client =
            CreateClient();

        client.DefaultRequestHeaders.Add(
            TestOmitTenantClaimHeader,
            "true");

        client.DefaultRequestHeaders.Add(
            TestScopesHeader,
            string.Join(
                ' ',
                scopes));

        return client;
    }

    public async Task InitializeDatabaseAsync()
    {
        await ExecuteDbAsync(
            async db =>
            {
                await db.Database
                    .EnsureCreatedAsync();
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

            await db.Database
                .EnsureCreatedAsync();

            await operation(
                db);
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

            await db.Database
                .EnsureCreatedAsync();

            return await operation(
                db);
        }
        finally
        {
            _databaseLock.Release();
        }
    }

    protected override void Dispose(
        bool disposing)
    {
        if (disposing)
        {
            _databaseLock.Dispose();
            _connection.Dispose();
        }

        base.Dispose(
            disposing);
    }

    private sealed class TestAuthenticationHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(
                options,
                logger,
                encoder)
        {
        }

        protected override Task<AuthenticateResult>
            HandleAuthenticateAsync()
        {
            if (Request.Headers.TryGetValue(
                    TestUnauthenticatedHeader,
                    out var unauthenticatedHeader) &&
                string.Equals(
                    unauthenticatedHeader.ToString(),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(
                    AuthenticateResult.NoResult());
            }

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

            var scopes =
                "Orders.Read Orders.Write";

            if (Request.Headers.TryGetValue(
                    TestScopesHeader,
                    out var scopesHeader))
            {
                scopes =
                    scopesHeader.ToString();
            }

            var claims =
                new List<Claim>
                {
                    new(
                        ClaimTypes.NameIdentifier,
                        $"e2e-user-{tenantId}"),

                    new(
                        ClaimTypes.Name,
                        "E2E Test User")
                };

            var omitTenantClaim =
                Request.Headers.TryGetValue(
                    TestOmitTenantClaimHeader,
                    out var omitTenantHeader) &&
                string.Equals(
                    omitTenantHeader.ToString(),
                    "true",
                    StringComparison.OrdinalIgnoreCase);

            if (!omitTenantClaim)
            {
                var tenantClaim =
                    tenantId.ToString();

                if (Request.Headers.TryGetValue(
                        TestTenantClaimHeader,
                        out var tenantClaimHeader))
                {
                    tenantClaim =
                        tenantClaimHeader.ToString();
                }

                claims.Add(
                    new Claim(
                        "tenant_id",
                        tenantClaim));
            }

            if (!string.IsNullOrWhiteSpace(
                    scopes))
            {
                claims.Add(
                    new Claim(
                        "scp",
                        scopes));
            }

            var identity =
                new ClaimsIdentity(
                    claims,
                    TestAuthenticationScheme);

            var principal =
                new ClaimsPrincipal(
                    identity);

            var ticket =
                new AuthenticationTicket(
                    principal,
                    TestAuthenticationScheme);

            return Task.FromResult(
                AuthenticateResult.Success(
                    ticket));
        }
    }
}