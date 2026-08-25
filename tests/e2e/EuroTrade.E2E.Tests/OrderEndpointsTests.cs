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
        _factory =
            factory;

        _client =
            factory.CreateClient();
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
            await PostOrderAsync(
                _client,
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<
                    CreateOrderResponse>();

        Assert.NotNull(
            created);

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
                .ReadFromJsonAsync<
                    GetOrderResponse>();

        Assert.NotNull(
            order);

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

        var createResponse =
            await PostOrderAsync(
                tenantAClient,
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<
                    CreateOrderResponse>();

        Assert.NotNull(
            created);

        Assert.NotEqual(
            Guid.Empty,
            created.OrderId);

        var tenantAResponse =
            await tenantAClient.GetAsync(
                $"/api/orders/{created.OrderId}");

        Assert.Equal(
            HttpStatusCode.OK,
            tenantAResponse.StatusCode);

        var tenantAOrder =
            await tenantAResponse.Content
                .ReadFromJsonAsync<
                    GetOrderResponse>();

        Assert.NotNull(
            tenantAOrder);

        Assert.Equal(
            tenantA,
            tenantAOrder.TenantId);

        Assert.Equal(
            created.OrderId,
            tenantAOrder.OrderId);

        var tenantBResponse =
            await tenantBClient.GetAsync(
                $"/api/orders/{created.OrderId}");

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
            await PostOrderAsync(
                _client,
                request);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<
                    CreateOrderResponse>();

        Assert.NotNull(
            created);

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

        Assert.NotNull(
            outboxMessage);

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
                                        outboxMessage.Id
                                            .ToString())),
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
    public async Task Retrying_create_order_with_same_key_returns_same_order()
    {
        var request =
            new CreateOrderRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                5);

        const string idempotencyKey =
            "retry-create-order";

        var firstResponse =
            await PostOrderAsync(
                _client,
                request,
                idempotencyKey);

        var secondResponse =
            await PostOrderAsync(
                _client,
                request,
                idempotencyKey);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Created,
            secondResponse.StatusCode);

        var first =
            await firstResponse.Content
                .ReadFromJsonAsync<
                    CreateOrderResponse>();

        var second =
            await secondResponse.Content
                .ReadFromJsonAsync<
                    CreateOrderResponse>();

        Assert.NotNull(
            first);

        Assert.NotNull(
            second);

        Assert.Equal(
            first.OrderId,
            second.OrderId);

        var orderCount =
            await _factory.ExecuteDbAsync(
                db =>
                    db.Orders.CountAsync(
                        order =>
                            order.Id ==
                            first.OrderId));

        Assert.Equal(
            1,
            orderCount);

        var idempotencyCount =
            await _factory.ExecuteDbAsync(
                db =>
                    db.IdempotencyRecords
                        .CountAsync(
                            record =>
                                record.TenantId ==
                                EuroTradeApiFactory
                                    .DefaultTenantId &&
                                record.IdempotencyKey ==
                                idempotencyKey));

        Assert.Equal(
            1,
            idempotencyCount);

        var outboxCount =
            await _factory.ExecuteDbAsync(
                db =>
                    db.OutboxMessages
                        .CountAsync(
                            message =>
                                message.Payload.Contains(
                                    first.OrderId
                                        .ToString())));

        Assert.Equal(
            1,
            outboxCount);
    }

    [Fact]
    public async Task Reusing_same_key_for_different_request_returns_conflict()
    {
        const string idempotencyKey =
            "conflicting-order-key";

        var firstRequest =
            new CreateOrderRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                2);

        var secondRequest =
            new CreateOrderRequest(
                firstRequest.CustomerId,
                firstRequest.ProductId,
                99);

        var firstResponse =
            await PostOrderAsync(
                _client,
                firstRequest,
                idempotencyKey);

        var secondResponse =
            await PostOrderAsync(
                _client,
                secondRequest,
                idempotencyKey);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondResponse.StatusCode);
    }

    [Fact]
    public async Task Same_idempotency_key_can_be_used_by_different_tenants()
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

        const string idempotencyKey =
            "shared-tenant-key";

        var request =
            new CreateOrderRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                4);

        var tenantAResponse =
            await PostOrderAsync(
                tenantAClient,
                request,
                idempotencyKey);

        var tenantBResponse =
            await PostOrderAsync(
                tenantBClient,
                request,
                idempotencyKey);

        Assert.Equal(
            HttpStatusCode.Created,
            tenantAResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Created,
            tenantBResponse.StatusCode);

        var tenantAOrder =
            await tenantAResponse.Content
                .ReadFromJsonAsync<
                    CreateOrderResponse>();

        var tenantBOrder =
            await tenantBResponse.Content
                .ReadFromJsonAsync<
                    CreateOrderResponse>();

        Assert.NotNull(
            tenantAOrder);

        Assert.NotNull(
            tenantBOrder);

        Assert.NotEqual(
            tenantAOrder.OrderId,
            tenantBOrder.OrderId);

        var idempotencyRecords =
            await _factory.ExecuteDbAsync(
                db =>
                    db.IdempotencyRecords
                        .Where(record =>
                            record.IdempotencyKey ==
                            idempotencyKey)
                        .ToListAsync());

        Assert.Equal(
            2,
            idempotencyRecords.Count);

        Assert.Contains(
            idempotencyRecords,
            record =>
                record.TenantId ==
                tenantA);

        Assert.Contains(
            idempotencyRecords,
            record =>
                record.TenantId ==
                tenantB);
    }

    [Fact]
    public async Task Create_order_without_idempotency_key_returns_bad_request()
    {
        var request =
            new CreateOrderRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1);

        var response =
            await _client.PostAsJsonAsync(
                "/api/orders",
                request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
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

    private static async Task<HttpResponseMessage>
        PostOrderAsync(
            HttpClient client,
            CreateOrderRequest request,
            string? idempotencyKey = null)
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
            idempotencyKey ??
            Guid.NewGuid().ToString());

        return await client.SendAsync(
            message);
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
            "Timed out waiting for the expected " +
            "background-processing state.");

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