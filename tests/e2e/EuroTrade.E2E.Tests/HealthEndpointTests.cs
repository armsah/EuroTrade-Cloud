using System.Net;

namespace EuroTrade.E2E.Tests;

public sealed class HealthEndpointTests
    : IClassFixture<EuroTradeApiFactory>,
      IAsyncLifetime
{
    private readonly EuroTradeApiFactory _factory;
    private readonly HttpClient _client;

    public HealthEndpointTests(
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
    public async Task Liveness_returns_ok()
    {
        var response =
            await _client.GetAsync(
                "/health/live");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task Readiness_returns_ok_when_database_is_available()
    {
        var response =
            await _client.GetAsync(
                "/health/ready");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }
}