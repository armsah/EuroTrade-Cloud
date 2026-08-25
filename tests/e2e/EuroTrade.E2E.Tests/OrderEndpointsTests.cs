using System.Net;
using System.Net.Http.Json;

using Microsoft.EntityFrameworkCore;

namespace EuroTrade.E2E.Tests;

public sealed class OrderEndpointsTests
    : IClassFixture<EuroTradeApiFactory>,
      IAsyncLifetime
{
    private readonly EuroTradeApiFactory _factory;
    private readonly HttpClient _client;

    public OrderEndpointsTests(
        EuroTradeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
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
    public async Task Create_order_then_get_order_returns_persisted_order()
    {
        var request =
            new CreateOrderRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                5);

        var createResponse =
            await _client.PostAsJsonAsync(
                "/api/orders",
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<CreateOrderResponse>();

        Assert.NotNull(created);

        Assert.NotEqual(
            Guid.Empty,
            created.OrderId);

        var getResponse =
            await _client.GetAsync(
                $"/api/orders/{created.OrderId}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        var order =
            await getResponse.Content
                .ReadFromJsonAsync<GetOrderResponse>();

        Assert.NotNull(order);

        Assert.Equal(
            created.OrderId,
            order.OrderId);

        Assert.Equal(
            EuroTradeApiFactory.DefaultTenantId,
            order.TenantId);

        Assert.Equal(
            request.CustomerId,
            order.CustomerId);

        Assert.Equal(
            request.ProductId,
            order.ProductId);

        Assert.Equal(
            request.Quantity,
            order.Quantity);

        Assert.Equal(
            "Pending",
            order.Status);
    }

    [Fact]
    public async Task Tenant_cannot_retrieve_order_owned_by_another_tenant()
    {
        var tenantA =
            Guid.Parse(
                "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var tenantB =
            Guid.Parse(
                "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        using var tenantAClient =
            _factory.CreateClientForTenant(
                tenantA);

        using var tenantBClient =
            _factory.CreateClientForTenant(
                tenantB);

        var request =
            new CreateOrderRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                5);

        // Tenant A creates the order.
        var createResponse =
            await tenantAClient.PostAsJsonAsync(
                "/api/orders",
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<CreateOrderResponse>();

        Assert.NotNull(created);

        Assert.NotEqual(
            Guid.Empty,
            created.OrderId);

        // Tenant A can retrieve its own order.
        var tenantAResponse =
            await tenantAClient.GetAsync(
                $"/api/orders/{created.OrderId}");

        Assert.Equal(
            HttpStatusCode.OK,
            tenantAResponse.StatusCode);

        var tenantAOrder =
            await tenantAResponse.Content
                .ReadFromJsonAsync<GetOrderResponse>();

        Assert.NotNull(tenantAOrder);

        Assert.Equal(
            tenantA,
            tenantAOrder.TenantId);

        Assert.Equal(
            created.OrderId,
            tenantAOrder.OrderId);

        // Tenant B attempts to retrieve Tenant A's order.
        var tenantBResponse =
            await tenantBClient.GetAsync(
                $"/api/orders/{created.OrderId}");

        // Deliberately return 404 rather than revealing that
        // another tenant owns the requested resource.
        Assert.Equal(
            HttpStatusCode.NotFound,
            tenantBResponse.StatusCode);
    }

    [Fact]
    public async Task Create_order_publishes_outbox_message_and_records_inbox_message()
    {
        var request =
            new CreateOrderRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                3);

        var createResponse =
            await _client.PostAsJsonAsync(
                "/api/orders",
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<CreateOrderResponse>();

        Assert.NotNull(created);

        Assert.NotEqual(
            Guid.Empty,
            created.OrderId);

        var outboxMessage =
            await WaitForAsync(
                async () =>
                    await _factory.ExecuteDbAsync(
                        async db =>
                            await db.OutboxMessages
                                .Where(message =>
                                    message.MessageType ==
                                    nameof(
                                        EuroTrade.Application
                                            .Orders.Events
                                            .OrderCreated))
                                .Where(message =>
                                    message.Payload.Contains(
                                        created.OrderId.ToString()))
                                .FirstOrDefaultAsync()),
                message =>
                    message is not null);

        Assert.NotNull(outboxMessage);

        Assert.Equal(
            nameof(
                EuroTrade.Application
                    .Orders.Events
                    .OrderCreated),
            outboxMessage.MessageType);

        Assert.Contains(
            created.OrderId.ToString(),
            outboxMessage.Payload);

        var publishedOutboxMessage =
            await WaitForAsync(
                async () =>
                    await _factory.ExecuteDbAsync(
                        async db =>
                            await db.OutboxMessages
                                .SingleOrDefaultAsync(
                                    message =>
                                        message.Id ==
                                        outboxMessage.Id)),
                message =>
                    message?.PublishedAt is not null);

        Assert.NotNull(
            publishedOutboxMessage);

        Assert.NotNull(
            publishedOutboxMessage.PublishedAt);

        Assert.Null(
            publishedOutboxMessage.Error);

        var inboxMessage =
            await WaitForAsync(
                async () =>
                    await _factory.ExecuteDbAsync(
                        async db =>
                            await db.InboxMessages
                                .SingleOrDefaultAsync(
                                    message =>
                                        message.MessageId ==
                                        outboxMessage.Id.ToString())),
                message =>
                    message is not null &&
                    message.ProcessedAt is not null);

        Assert.NotNull(
            inboxMessage);

        Assert.Equal(
            outboxMessage.Id.ToString(),
            inboxMessage.MessageId);

        Assert.NotNull(
            inboxMessage.ProcessedAt);
    }

    [Fact]
    public async Task Get_unknown_order_returns_not_found()
    {
        var response =
            await _client.GetAsync(
                $"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    private static async Task<T> WaitForAsync<T>(
        Func<Task<T>> operation,
        Func<T, bool> condition,
        int timeoutMilliseconds = 10000,
        int pollingMilliseconds = 100)
    {
        var deadline =
            DateTime.UtcNow.AddMilliseconds(
                timeoutMilliseconds);

        while (DateTime.UtcNow < deadline)
        {
            var result =
                await operation();

            if (condition(result))
            {
                return result;
            }

            await Task.Delay(
                pollingMilliseconds);
        }

        var finalResult =
            await operation();

        Assert.True(
            condition(finalResult),
            "Timed out waiting for the expected background-processing state.");

        return finalResult;
    }

    private sealed record CreateOrderRequest(
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