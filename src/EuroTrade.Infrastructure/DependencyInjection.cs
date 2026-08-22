using Azure.Identity;
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

        if (configuration["Database:Provider"] == "Sqlite")
        {
            services.AddDbContextFactory<OrdersDbContext>(options =>
                options.UseSqlite(connectionString));
        }
        else
        {
            services.AddDbContextFactory<OrdersDbContext>(options =>
                options.UseNpgsql(connectionString));
        }

        services.AddScoped<IOrderRepository, EfOrderRepository>();
        services.AddScoped<IOrderWriter, EfOrderWriter>();

        var serviceBusNamespace =
            configuration["ServiceBus:FullyQualifiedNamespace"];

        var queueName =
            configuration["ServiceBus:QueueName"];

        // Local development fallback.
        // AKS production uses the Service Bus namespace
        // together with Azure Workload Identity.
        if (string.IsNullOrWhiteSpace(serviceBusNamespace) ||
            string.IsNullOrWhiteSpace(queueName))
        {
            services.AddSingleton<InMemoryEventBus>();

            services.AddSingleton<IEventBus>(
                provider =>
                    provider.GetRequiredService<InMemoryEventBus>());

            services.AddSingleton<IEventConsumer>(
                provider =>
                    provider.GetRequiredService<InMemoryEventBus>());

            services.AddHostedService<OrderEventWorker>();

            return services;
        }

        var serviceBusClient =
            new ServiceBusClient(
                serviceBusNamespace,
                new DefaultAzureCredential());

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