using EuroTrade.Application.Messaging;
using EuroTrade.Application.Orders;
using EuroTrade.Infrastructure.Messaging;
using EuroTrade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EuroTrade.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrdersDb")
            ?? throw new InvalidOperationException(
                "Connection string 'OrdersDb' was not configured.");

        services.AddDbContext<OrdersDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IOrderRepository, EfOrderRepository>();

        services.AddSingleton<InMemoryEventBus>();

        services.AddSingleton<IEventBus>(
            serviceProvider =>
                serviceProvider.GetRequiredService<InMemoryEventBus>());

        services.AddSingleton<IEventConsumer>(
            serviceProvider =>
                serviceProvider.GetRequiredService<InMemoryEventBus>());

        services.AddHostedService<OrderEventWorker>();

        return services;
    }
}