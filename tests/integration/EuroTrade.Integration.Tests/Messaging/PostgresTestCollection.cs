using Testcontainers.PostgreSql;

namespace EuroTrade.Integration.Tests.Messaging;

[CollectionDefinition(
    "Postgres integration",
    DisableParallelization = true)]
public sealed class PostgresTestCollection
    : ICollectionFixture<PostgresTestFixture>
{
}

public sealed class PostgresTestFixture
    : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17")
            .WithDatabase("eurotrade_test")
            .WithUsername("eurotrade")
            .WithPassword("eurotrade-test-password")
            .Build();

    public string ConnectionString =>
        _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}