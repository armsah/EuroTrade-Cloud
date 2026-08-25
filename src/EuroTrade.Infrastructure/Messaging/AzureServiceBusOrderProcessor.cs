using System.Diagnostics;
using System.Text.Json;

using Azure.Messaging.ServiceBus;

using EuroTrade.Application.Orders.Events;
using EuroTrade.Application.Telemetry;

using EuroTrade.Infrastructure.Persistence;
using EuroTrade.Infrastructure.Persistence.Inbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EuroTrade.Infrastructure.Messaging;

public sealed class AzureServiceBusOrderProcessor(
    ServiceBusProcessor processor,
    IDbContextFactory<OrdersDbContext> dbContextFactory,
    InboxMessageStore inboxMessageStore,
    IConfiguration configuration,
    ILogger<AzureServiceBusOrderProcessor> logger)
    : IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task StartAsync(
        CancellationToken cancellationToken)
    {
        processor.ProcessMessageAsync +=
            ProcessMessageAsync;

        processor.ProcessErrorAsync +=
            ProcessErrorAsync;

        await processor.StartProcessingAsync(
            cancellationToken);

        logger.LogInformation(
            "Azure Service Bus order processor started. " +
            "AutoCompleteMessages: {AutoCompleteMessages}",
            processor.AutoCompleteMessages);
    }

    public async Task StopAsync(
        CancellationToken cancellationToken)
    {
        await processor.StopProcessingAsync(
            cancellationToken);

        processor.ProcessMessageAsync -=
            ProcessMessageAsync;

        processor.ProcessErrorAsync -=
            ProcessErrorAsync;

        logger.LogInformation(
            "Azure Service Bus order processor stopped.");
    }

    private async Task ProcessMessageAsync(
        ProcessMessageEventArgs args)
    {
        var message =
            args.Message;

        var cancellationToken =
            args.CancellationToken;

        var parentContext =
            CreateParentContext(
                message);

        using var activity =
            EuroTradeActivitySource.Source.StartActivity(
                "ProcessOrderCreated",
                ActivityKind.Consumer,
                parentContext);

        activity?.SetTag(
            "messaging.system",
            "azure_service_bus");

        activity?.SetTag(
            "messaging.destination.name",
            message.Subject);

        activity?.SetTag(
            "messaging.message.id",
            message.MessageId);

        activity?.SetTag(
            "messaging.operation.type",
            "process");

        try
        {
            // ================================================
            // Deserialize / validate
            // ================================================

            object? domainEvent;

            try
            {
                var eventType =
                    message.ApplicationProperties.TryGetValue(
                        "eventType",
                        out var eventTypeValue)
                            ? eventTypeValue?.ToString()
                            : message.Subject;

                domainEvent =
                    eventType switch
                    {
                        nameof(OrderCreated) =>
                            JsonSerializer.Deserialize<OrderCreated>(
                                message.Body.ToString(),
                                JsonOptions),

                        _ => null
                    };
            }
            catch (JsonException exception)
            {
                logger.LogWarning(
                    exception,
                    "Service Bus message {MessageId} contains " +
                    "an invalid event payload. Dead-lettering.",
                    message.MessageId);

                activity?.SetStatus(
                    ActivityStatusCode.Error,
                    "Invalid event payload.");

                await args.DeadLetterMessageAsync(
                    message,
                    "InvalidEventPayload",
                    exception.Message,
                    cancellationToken);

                return;
            }

            if (domainEvent is null)
            {
                logger.LogWarning(
                    "Service Bus message {MessageId} contains " +
                    "an unsupported or missing event type. " +
                    "Dead-lettering.",
                    message.MessageId);

                activity?.SetStatus(
                    ActivityStatusCode.Error,
                    "Unknown event type.");

                await args.DeadLetterMessageAsync(
                    message,
                    "UnknownEventType",
                    "Unsupported or missing event type.",
                    cancellationToken);

                return;
            }

            // ================================================
            // Process domain event
            // ================================================

            switch (domainEvent)
            {
                case OrderCreated orderCreated:

                    await ProcessOrderCreatedAsync(
                        orderCreated,
                        message,
                        cancellationToken);

                    break;

                default:

                    logger.LogWarning(
                        "Unsupported deserialized event type " +
                        "{EventType}. Message {MessageId} " +
                        "will be dead-lettered.",
                        domainEvent.GetType().Name,
                        message.MessageId);

                    activity?.SetStatus(
                        ActivityStatusCode.Error,
                        "Unsupported event type.");

                    await args.DeadLetterMessageAsync(
                        message,
                        "UnsupportedEventType",
                        $"Unsupported event type: " +
                        $"{domainEvent.GetType().Name}",
                        cancellationToken);

                    return;
            }

            // ================================================
            // Successful settlement
            // ================================================

            // IMPORTANT:
            // Processing and inbox persistence happen BEFORE
            // message completion.
            await args.CompleteMessageAsync(
                message,
                cancellationToken);

            activity?.SetStatus(
                ActivityStatusCode.Ok);

            logger.LogInformation(
                "Service Bus message {MessageId} " +
                "completed successfully.",
                message.MessageId);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            logger.LogError(
                exception,
                "Error processing Service Bus message " +
                "{MessageId}. DeliveryCount: {DeliveryCount}. " +
                "The message will be abandoned for retry.",
                message.MessageId,
                message.DeliveryCount);

            try
            {
                await args.AbandonMessageAsync(
                    message,
                    cancellationToken:
                        cancellationToken);
            }
            catch (Exception settlementException)
            {
                logger.LogError(
                    settlementException,
                    "Failed to abandon Service Bus message " +
                    "{MessageId}.",
                    message.MessageId);
            }
        }
    }

    private async Task ProcessOrderCreatedAsync(
        OrderCreated orderCreated,
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        using var activity =
            EuroTradeActivitySource.Source.StartActivity(
                "HandleOrderCreated");

        activity?.SetTag(
            "order.id",
            orderCreated.OrderId);

        activity?.SetTag(
            "order.tenant_id",
            orderCreated.TenantId);

        activity?.SetTag(
            "order.customer_id",
            orderCreated.CustomerId);

        activity?.SetTag(
            "order.product_id",
            orderCreated.ProductId);

        activity?.SetTag(
            "order.quantity",
            orderCreated.Quantity);

        logger.LogInformation(
            "OrderCreated event received from Azure Service Bus. " +
            "OrderId: {OrderId}, TenantId: {TenantId}, " +
            "MessageId: {MessageId}, " +
            "DeliveryCount: {DeliveryCount}",
            orderCreated.OrderId,
            orderCreated.TenantId,
            message.MessageId,
            message.DeliveryCount);

        var forceFailure =
            configuration.GetValue<bool>(
                "ServiceBus:ForceProcessingFailure");

        if (forceFailure)
        {
            throw new InvalidOperationException(
                "Intentional P6 retry/DLQ test failure.");
        }
        var recorded =
            await RecordInboxMessageAsync(
                message.MessageId,
                cancellationToken);

        if (!recorded)
        {
            logger.LogInformation(
                "Service Bus message {MessageId} " +
                "was already processed.",
                message.MessageId);
        }
    }


    private async Task<bool> RecordInboxMessageAsync(
      string messageId,
      CancellationToken cancellationToken)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        return await inboxMessageStore.TryRecordAsync(
            dbContext,
            messageId,
            cancellationToken);
    }

    private Task ProcessErrorAsync(
        ProcessErrorEventArgs args)
    {
        logger.LogError(
            args.Exception,
            "Azure Service Bus processor error. " +
            "ErrorSource: {ErrorSource}, " +
            "EntityPath: {EntityPath}, " +
            "Namespace: {Namespace}",
            args.ErrorSource,
            args.EntityPath,
            args.FullyQualifiedNamespace);

        return Task.CompletedTask;
    }

    private static ActivityContext CreateParentContext(
        ServiceBusReceivedMessage message)
    {
        if (!message.ApplicationProperties.TryGetValue(
                "Diagnostic-Id",
                out var diagnosticIdValue))
        {
            return default;
        }

        var diagnosticId =
            diagnosticIdValue?.ToString();

        if (string.IsNullOrWhiteSpace(
                diagnosticId))
        {
            return default;
        }

        var traceState =
            message.ApplicationProperties.TryGetValue(
                "TraceState",
                out var traceStateValue)
                    ? traceStateValue?.ToString()
                    : null;

        return ActivityContext.TryParse(
            diagnosticId,
            traceState,
            out var parentContext)
                ? parentContext
                : default;
    }
}