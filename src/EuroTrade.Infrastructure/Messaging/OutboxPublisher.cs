using System.Diagnostics;
using System.Text.Json;

using EuroTrade.Application.Messaging;
using EuroTrade.Application.Orders.Events;
using EuroTrade.Application.Telemetry;

using EuroTrade.Infrastructure.Observability;
using EuroTrade.Infrastructure.Persistence;
using EuroTrade.Infrastructure.Persistence.Outbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EuroTrade.Infrastructure.Messaging;

public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<OutboxPublisher> logger)
    : BackgroundService
{
    private const int BatchSize = 20;

    private readonly int _maxAttempts =
        Math.Max(
            1,
            configuration.GetValue(
                "Outbox:MaxAttempts",
                5));

    private readonly double _baseRetryDelaySeconds =
        Math.Max(
            0.1,
            configuration.GetValue(
                "Outbox:BaseRetryDelaySeconds",
                2.0));

    private readonly double _maxRetryDelaySeconds =
        Math.Max(
            0.1,
            configuration.GetValue(
                "Outbox:MaxRetryDelaySeconds",
                300.0));

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Outbox publisher started. " +
            "MaxAttempts: {MaxAttempts}, " +
            "BaseRetryDelaySeconds: {BaseRetryDelaySeconds}, " +
            "MaxRetryDelaySeconds: {MaxRetryDelaySeconds}",
            _maxAttempts,
            _baseRetryDelaySeconds,
            _maxRetryDelaySeconds);

        using var timer =
            new PeriodicTimer(
                TimeSpan.FromSeconds(2));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingMessagesAsync(
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Error while publishing outbox messages.");
            }

            try
            {
                await timer.WaitForNextTickAsync(
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation(
            "Outbox publisher stopped.");
    }

    internal async Task PublishPendingMessagesAsync(
        CancellationToken cancellationToken)
    {
        using var scope =
            scopeFactory.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<OrdersDbContext>();

        var eventBus =
            scope.ServiceProvider
                .GetRequiredService<IEventBus>();

        // Capture total outstanding backlog before processing.
        // This includes messages waiting for their retry delay,
        // but excludes messages already published or permanently
        // marked as failed/poison.
        await UpdatePendingMessagesMetricAsync(
            dbContext,
            cancellationToken);

        if (dbContext.Database.IsNpgsql())
        {
            await PublishPostgresBatchAsync(
                dbContext,
                eventBus,
                cancellationToken);
        }
        else
        {
            await PublishNonPostgresBatchAsync(
                dbContext,
                eventBus,
                cancellationToken);
        }

        // Refresh after the batch so the gauge reflects the
        // resulting backlog rather than only its initial state.
        await UpdatePendingMessagesMetricAsync(
            dbContext,
            cancellationToken);
    }

    private async Task PublishPostgresBatchAsync(
        OrdersDbContext dbContext,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            /*
             * The transaction remains open while the selected
             * messages are published.
             *
             * FOR UPDATE locks rows claimed by this replica.
             *
             * SKIP LOCKED allows other replicas to immediately
             * skip those rows and claim a different batch.
             *
             * Retry eligibility additionally excludes:
             *
             * - successfully published messages
             * - poison/failed messages
             * - messages whose retry delay has not elapsed
             */
            var messages =
                await dbContext.OutboxMessages
                    .FromSqlInterpolated(
                        $"""
                        SELECT *
                        FROM outbox_messages
                        WHERE "PublishedAt" IS NULL
                          AND "FailedAt" IS NULL
                          AND (
                              "NextAttemptAt" IS NULL
                              OR "NextAttemptAt" <= NOW()
                          )
                        ORDER BY "CreatedAt"
                        LIMIT {BatchSize}
                        FOR UPDATE SKIP LOCKED
                        """)
                    .AsTracking()
                    .ToListAsync(
                        cancellationToken);

            if (messages.Count == 0)
            {
                await transaction.CommitAsync(
                    cancellationToken);

                return;
            }

            logger.LogDebug(
                "Claimed {MessageCount} PostgreSQL " +
                "outbox messages using FOR UPDATE SKIP LOCKED.",
                messages.Count);

            await PublishMessagesAsync(
                dbContext,
                eventBus,
                messages,
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);
        }
        catch
        {
            await RollbackSafelyAsync(
                transaction,
                cancellationToken);

            throw;
        }
    }

    private async Task PublishNonPostgresBatchAsync(
        OrdersDbContext dbContext,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        /*
         * SQLite is used by local/E2E tests.
         *
         * SQLite cannot use PostgreSQL FOR UPDATE SKIP LOCKED
         * and has limited DateTimeOffset query translation,
         * so retry eligibility and ordering are evaluated
         * in memory.
         */
        var now =
            DateTimeOffset.UtcNow;

        var pendingMessages =
            await dbContext.OutboxMessages
                .Where(message =>
                    message.PublishedAt == null &&
                    message.FailedAt == null)
                .ToListAsync(
                    cancellationToken);

        var messages =
            pendingMessages
                .Where(message =>
                    message.NextAttemptAt is null ||
                    message.NextAttemptAt <= now)
                .OrderBy(message =>
                    message.CreatedAt)
                .Take(BatchSize)
                .ToList();

        await PublishMessagesAsync(
            dbContext,
            eventBus,
            messages,
            cancellationToken);
    }

    private async Task PublishMessagesAsync(
        OrdersDbContext dbContext,
        IEventBus eventBus,
        IReadOnlyCollection<OutboxMessage> messages,
        CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            var attemptStartedAt =
                DateTimeOffset.UtcNow;

            message.AttemptCount +=
                1;

            message.LastAttemptAt =
                attemptStartedAt;

            try
            {
                await PublishMessageAsync(
                    eventBus,
                    message,
                    cancellationToken);

                message.PublishedAt =
                    DateTimeOffset.UtcNow;

                message.NextAttemptAt =
                    null;

                message.LastError =
                    null;

                message.FailedAt =
                    null;

                await dbContext.SaveChangesAsync(
                    cancellationToken);

                logger.LogInformation(
                    "Published outbox message {MessageId}. " +
                    "MessageType: {MessageType}. " +
                    "Attempt: {AttemptCount}",
                    message.Id,
                    message.MessageType,
                    message.AttemptCount);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (PermanentOutboxMessageException exception)
            {
                await MarkPermanentFailureAsync(
                    dbContext,
                    message,
                    exception,
                    attemptStartedAt,
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                await MarkPermanentFailureAsync(
                    dbContext,
                    message,
                    exception,
                    attemptStartedAt,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                await RecordTransientFailureAsync(
                    dbContext,
                    message,
                    exception,
                    attemptStartedAt,
                    cancellationToken);
            }
        }
    }

    private static async Task PublishMessageAsync(
        IEventBus eventBus,
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        switch (message.MessageType)
        {
            case nameof(OrderCreated):
            {
                var orderCreated =
                    JsonSerializer.Deserialize<OrderCreated>(
                        message.Payload,
                        JsonOptions)
                    ?? throw new PermanentOutboxMessageException(
                        $"Could not deserialize outbox " +
                        $"message {message.Id}.");

                var parentContext =
                    CreateParentContext(
                        message);

                using var activity =
                    EuroTradeActivitySource.Source.StartActivity(
                        "PublishOrderCreated",
                        ActivityKind.Producer,
                        parentContext);

                activity?.SetTag(
                    "messaging.system",
                    "azure_service_bus");

                activity?.SetTag(
                    "messaging.destination.name",
                    "order-created");

                activity?.SetTag(
                    "messaging.message.id",
                    message.Id.ToString());

                activity?.SetTag(
                    "outbox.attempt_count",
                    message.AttemptCount);

                activity?.SetTag(
                    "order.id",
                    orderCreated.OrderId);

                activity?.SetTag(
                    "order.tenant_id",
                    orderCreated.TenantId);

                try
                {
                    await eventBus.PublishAsync(
                        orderCreated,
                        message.Id.ToString(),
                        cancellationToken);

                    activity?.SetStatus(
                        ActivityStatusCode.Ok);
                }
                catch (Exception exception)
                {
                    activity?.SetStatus(
                        ActivityStatusCode.Error,
                        exception.Message);

                    throw;
                }

                break;
            }

            default:
                throw new PermanentOutboxMessageException(
                    $"Unsupported outbox message type: " +
                    $"{message.MessageType}");
        }
    }

    private async Task RecordTransientFailureAsync(
        OrdersDbContext dbContext,
        OutboxMessage message,
        Exception exception,
        DateTimeOffset attemptStartedAt,
        CancellationToken cancellationToken)
    {
        // Count actual event-bus publishing failures.
        // Message type is bounded-cardinality and safe as
        // a metric dimension.
        EuroTradeMetrics.OutboxPublishFailures.Add(
            1,
            new KeyValuePair<string, object?>(
                "message.type",
                message.MessageType));

        message.LastError =
            exception.Message;

        if (message.AttemptCount >=
            _maxAttempts)
        {
            message.FailedAt =
                attemptStartedAt;

            message.NextAttemptAt =
                null;

            await dbContext.SaveChangesAsync(
                cancellationToken);

            /*
             * This critical log is the operational alert hook.
             * Application Insights / Azure Monitor can alert
             * on this log level and message.
             */
            logger.LogCritical(
                exception,
                "Outbox message {MessageId} entered the " +
                "poison state after {AttemptCount} attempts. " +
                "MessageType: {MessageType}",
                message.Id,
                message.AttemptCount,
                message.MessageType);

            return;
        }

        var delay =
            CalculateRetryDelay(
                message.AttemptCount);

        message.NextAttemptAt =
            attemptStartedAt.Add(
                delay);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        logger.LogWarning(
            exception,
            "Failed to publish outbox message {MessageId}. " +
            "Attempt {AttemptCount}/{MaxAttempts}. " +
            "Next retry at {NextAttemptAt}.",
            message.Id,
            message.AttemptCount,
            _maxAttempts,
            message.NextAttemptAt);
    }

    private async Task MarkPermanentFailureAsync(
        OrdersDbContext dbContext,
        OutboxMessage message,
        Exception exception,
        DateTimeOffset attemptStartedAt,
        CancellationToken cancellationToken)
    {
        message.LastError =
            exception.Message;

        message.FailedAt =
            attemptStartedAt;

        message.NextAttemptAt =
            null;

        await dbContext.SaveChangesAsync(
            cancellationToken);

        logger.LogCritical(
            exception,
            "Outbox message {MessageId} entered the " +
            "poison state because it cannot be processed. " +
            "MessageType: {MessageType}.",
            message.Id,
            message.MessageType);
    }

    private static async Task UpdatePendingMessagesMetricAsync(
        OrdersDbContext dbContext,
        CancellationToken cancellationToken)
    {
        /*
         * The observable metric itself must remain cheap and
         * synchronous. Database I/O therefore happens here in
         * the existing publisher loop, and EuroTradeMetrics
         * exposes only the most recently sampled value.
         *
         * Pending means:
         *
         * - not successfully published
         * - not permanently failed/poison
         *
         * Messages waiting for NextAttemptAt are intentionally
         * included because they remain part of the backlog.
         */
        var pendingCount =
            await dbContext.OutboxMessages
                .AsNoTracking()
                .LongCountAsync(
                    message =>
                        message.PublishedAt == null &&
                        message.FailedAt == null,
                    cancellationToken);

        EuroTradeMetrics.SetOutboxPendingMessages(
            pendingCount);
    }

    private TimeSpan CalculateRetryDelay(
        int attemptCount)
    {
        /*
         * Exponential backoff:
         *
         * base * 2^(attempt - 1)
         *
         * followed by bounded jitter between 80% and
         * 100% of that value. The result never exceeds
         * MaxRetryDelaySeconds.
         */
        var exponent =
            Math.Max(
                0,
                attemptCount - 1);

        var exponentialDelay =
            _baseRetryDelaySeconds *
            Math.Pow(
                2,
                Math.Min(
                    exponent,
                    30));

        var cappedDelay =
            Math.Min(
                exponentialDelay,
                _maxRetryDelaySeconds);

        var jitterFactor =
            0.8 +
            Random.Shared.NextDouble() * 0.2;

        var delaySeconds =
            Math.Min(
                cappedDelay * jitterFactor,
                _maxRetryDelaySeconds);

        return TimeSpan.FromSeconds(
            delaySeconds);
    }

    private static async Task RollbackSafelyAsync(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(
                cancellationToken);
        }
        catch
        {
            /*
             * Preserve the original publishing/database
             * exception if rollback itself fails.
             */
        }
    }

    private static ActivityContext CreateParentContext(
        OutboxMessage message)
    {
        if (!string.IsNullOrWhiteSpace(
                message.TraceParent))
        {
            if (ActivityContext.TryParse(
                    message.TraceParent,
                    message.TraceState,
                    out var parentContext))
            {
                return parentContext;
            }
        }

        return default;
    }

    private sealed class PermanentOutboxMessageException(
        string message)
        : Exception(message);
}