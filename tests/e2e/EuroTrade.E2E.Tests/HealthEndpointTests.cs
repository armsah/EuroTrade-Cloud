using System.Net;

namespace EuroTrade.E2E.Tests;

public sealed class HealthEndpointTests
    : IClassFixture<EuroTradeApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(
        EuroTradeApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_health_returns_ok()
    {
        var response =
            await _client.GetAsync("/health");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }
}