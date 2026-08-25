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
            services.AddDbContextFactory<OrdersDbContext>(
                options =>
                    options.UseSqlite(
                        connectionString));
        }
        else
        {
            services.AddDbContextFactory<OrdersDbContext>(
                options =>
                    options.UseNpgsql(
                        connectionString));
        }

        services.AddScoped<IOrderRepository, EfOrderRepository>();
        services.AddScoped<IOrderWriter, EfOrderWriter>();

        var serviceBusNamespace =
            configuration[
                "ServiceBus:FullyQualifiedNamespace"];

        var queueName =
            configuration[
                "ServiceBus:QueueName"];

        // ====================================================
        // Local development / E2E path
        // ====================================================

        if (string.IsNullOrWhiteSpace(
                serviceBusNamespace) ||
            string.IsNullOrWhiteSpace(
                queueName))
        {
            services.AddSingleton<InMemoryEventBus>();

            services.AddSingleton<IEventBus>(
                provider =>
                    provider.GetRequiredService<
                        InMemoryEventBus>());

            services.AddSingleton<IEventConsumer>(
                provider =>
                    provider.GetRequiredService<
                        InMemoryEventBus>());

            services.AddHostedService<OrderEventWorker>();
            services.AddHostedService<OutboxPublisher>();

            return services;
        }

        // ====================================================
        // Azure Service Bus production path
        // ====================================================

        var serviceBusClient =
            new ServiceBusClient(
                serviceBusNamespace,
                new DefaultAzureCredential());

        services.AddSingleton(
            serviceBusClient);

        var sender =
            serviceBusClient.CreateSender(
                queueName);

        services.AddSingleton(
            sender);

        services.AddSingleton<
            IEventBus,
            AzureServiceBusEventBus>();

        var processor =
            serviceBusClient.CreateProcessor(
                queueName,
                new ServiceBusProcessorOptions
                {
                    // Settlement is deliberately owned by
                    // AzureServiceBusOrderProcessor.
                    AutoCompleteMessages = false,

                    ReceiveMode =
                        ServiceBusReceiveMode.PeekLock,

                    // Keep processing deterministic initially.
                    // This can be increased after measuring
                    // throughput and DB contention.
                    MaxConcurrentCalls = 1,

                    // Allow long-running handlers to retain
                    // their Peek-Lock while processing.
                    MaxAutoLockRenewalDuration =
                        TimeSpan.FromMinutes(5)
                });

        services.AddSingleton(
            processor);

        services.AddHostedService<
            AzureServiceBusOrderProcessor>();

        services.AddHostedService<
            OutboxPublisher>();

        return services;
    }
}