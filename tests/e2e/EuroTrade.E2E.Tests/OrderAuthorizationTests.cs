using System.Net;
using System.Net.Http.Json;

namespace EuroTrade.E2E.Tests;

public sealed class OrderAuthorizationTests
    : IClassFixture<EuroTradeApiFactory>,
      IAsyncLifetime
{
    private readonly EuroTradeApiFactory _factory;

    public OrderAuthorizationTests(
        EuroTradeApiFactory factory)
    {
        _factory =
            factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Create_order_without_authentication_returns_unauthorized()
    {
        using var client =
            _factory.CreateUnauthenticatedClient();

        var request =
            new CreateOrderRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1);

        var response =
            await PostOrderAsync(
                client,
                request);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Create_order_with_read_scope_only_returns_forbidden()
    {
        using var client =
            _factory.CreateClientWithScopes(
                "Orders.Read");

        var request =
            new CreateOrderRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1);

        var response =
            await PostOrderAsync(
                client,
                request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task Create_order_with_write_scope_succeeds()
    {
        using var client =
            _factory.CreateClientWithScopes(
                "Orders.Write");

        var request =
            new CreateOrderRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1);

        var response =
            await PostOrderAsync(
                client,
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);
    }

    [Fact]
    public async Task Get_order_without_authentication_returns_unauthorized()
    {
        using var client =
            _factory.CreateUnauthenticatedClient();

        var response =
            await client.GetAsync(
                $"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Get_order_with_write_scope_only_returns_forbidden()
    {
        using var client =
            _factory.CreateClientWithScopes(
                "Orders.Write");

        var response =
            await client.GetAsync(
                $"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task Get_order_with_read_scope_is_authorized()
    {
        using var client =
            _factory.CreateClientWithScopes(
                "Orders.Read");

        var response =
            await client.GetAsync(
                $"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task Tenant_cannot_read_another_tenants_order_even_with_read_scope()
    {
        var tenantA =
            Guid.Parse(
                "cccccccc-cccc-cccc-cccc-cccccccccccc");

        var tenantB =
            Guid.Parse(
                "dddddddd-dddd-dddd-dddd-dddddddddddd");

        using var tenantAClient =
            _factory.CreateClientForTenantWithScopes(
                tenantA,
                "Orders.Read",
                "Orders.Write");

        using var tenantBClient =
            _factory.CreateClientForTenantWithScopes(
                tenantB,
                "Orders.Read");

        var request =
            new CreateOrderRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1);

        var createResponse =
            await PostOrderAsync(
                tenantAClient,
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<CreateOrderResponse>();

        Assert.NotNull(
            created);

        var ownerResponse =
            await tenantAClient.GetAsync(
                $"/api/orders/{created.OrderId}");

        Assert.Equal(
            HttpStatusCode.OK,
            ownerResponse.StatusCode);

        var otherTenantResponse =
            await tenantBClient.GetAsync(
                $"/api/orders/{created.OrderId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            otherTenantResponse.StatusCode);
    }

    private static async Task<HttpResponseMessage>
        PostOrderAsync(
            HttpClient client,
            CreateOrderRequest request)
    {
        using var message =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/orders")
            {
                Content =
                    JsonContent.Create(
                        request)
            };

        message.Headers.Add(
            "Idempotency-Key",
            Guid.NewGuid().ToString());

        return await client.SendAsync(
            message);
    }

    private sealed record CreateOrderRequest(
        Guid CustomerId,
        Guid ProductId,
        int Quantity);

    private sealed record CreateOrderResponse(
        Guid OrderId);
}