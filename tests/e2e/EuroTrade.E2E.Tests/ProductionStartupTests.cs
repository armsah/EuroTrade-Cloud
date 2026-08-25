using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EuroTrade.E2E.Tests;

public sealed class ProductionStartupTests
{
    [Fact]
    public void Production_startup_fails_when_azure_ad_configuration_is_missing()
    {
        using var factory =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(
                    builder =>
                    {
                        builder.UseEnvironment(
                            "Production");

                        builder.UseSetting(
                            "AzureAd:Instance",
                            string.Empty);

                        builder.UseSetting(
                            "AzureAd:TenantId",
                            string.Empty);

                        builder.UseSetting(
                            "AzureAd:ClientId",
                            string.Empty);
                    });

        var exception =
            Assert.ThrowsAny<Exception>(
                () =>
                {
                    using var client =
                        factory.CreateClient();
                });

        var message =
            exception.ToString();

        Assert.Contains(
            "Azure AD authentication configuration is incomplete.",
            message);

        Assert.Contains(
            "AzureAd:Instance",
            message);

        Assert.Contains(
            "AzureAd:TenantId",
            message);

        Assert.Contains(
            "AzureAd:ClientId",
            message);
    }
}