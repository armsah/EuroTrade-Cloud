using Azure.Messaging.ServiceBus;
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
        var connectionString =
            configuration.GetConnectionString("OrdersDb")
            ?? throw new InvalidOperationException(
                "Connection string 'OrdersDb' was not configured.");

        services.AddDbContextFactory<OrdersDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IOrderRepository, EfOrderRepository>();
        services.AddScoped<IOrderWriter, EfOrderWriter>();

        var serviceBusConnectionString =
            configuration["ServiceBus:ConnectionString"];

        var queueName =
            configuration["ServiceBus:QueueName"];

        if (string.IsNullOrWhiteSpace(serviceBusConnectionString) ||
            string.IsNullOrWhiteSpace(queueName))
        {
            services.AddSingleton<InMemoryEventBus>();
            services.AddSingleton<IEventBus>(
                provider => provider.GetRequiredService<InMemoryEventBus>());
            services.AddSingleton<IEventConsumer>(
                provider => provider.GetRequiredService<InMemoryEventBus>());

            services.AddHostedService<OrderEventWorker>();

            return services;
        }

        var serviceBusClient =
            new ServiceBusClient(serviceBusConnectionString);

        services.AddSingleton(serviceBusClient);

        services.AddSingleton(
            serviceBusClient.CreateSender(queueName));

        services.AddSingleton(
            serviceBusClient.CreateReceiver(queueName));

        services.AddSingleton<IEventBus, AzureServiceBusEventBus>();

        services.AddSingleton<IEventConsumer, AzureServiceBusEventConsumer>();

        services.AddHostedService<OrderEventWorker>();
        services.AddHostedService<OutboxPublisher>();

        return services;
    }
}