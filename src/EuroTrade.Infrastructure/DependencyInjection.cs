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
            configuration["ServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Service Bus connection string was not configured.");

        var queueName =
            configuration["ServiceBus:QueueName"]
            ?? throw new InvalidOperationException(
                "Service Bus queue name was not configured.");

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


