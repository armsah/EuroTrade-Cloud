using System.Net;
using System.Net.Http.Json;

namespace EuroTrade.E2E.Tests;

public sealed class OrderEndpointsTests
    : IClassFixture<EuroTradeApiFactory>
{
    private readonly HttpClient _client;

    public OrderEndpointsTests(EuroTradeApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_order_then_get_order_returns_persisted_order()
    {
        var request = new CreateOrderRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            5);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/orders",
            request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created = await createResponse.Content
            .ReadFromJsonAsync<CreateOrderResponse>();

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.OrderId);

        var getResponse = await _client.GetAsync(
            $"/api/orders/{created.OrderId}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        var order = await getResponse.Content
            .ReadFromJsonAsync<GetOrderResponse>();

        Assert.NotNull(order);

        Assert.Equal(created.OrderId, order.OrderId);
        Assert.Equal(request.TenantId, order.TenantId);
        Assert.Equal(request.CustomerId, order.CustomerId);
        Assert.Equal(request.ProductId, order.ProductId);
        Assert.Equal(request.Quantity, order.Quantity);
        Assert.Equal("Pending", order.Status);
    }

    private sealed record CreateOrderRequest(
        Guid TenantId,
        Guid CustomerId,
        Guid ProductId,
        int Quantity);

    private sealed record CreateOrderResponse(
        Guid OrderId);

    private sealed record GetOrderResponse(
        Guid OrderId,
        Guid TenantId,
        Guid CustomerId,
        Guid ProductId,
        int Quantity,
        string Status,
        DateTime CreatedAt);
}