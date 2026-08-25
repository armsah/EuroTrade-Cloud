using System.Diagnostics.Metrics;

namespace EuroTrade.Infrastructure.Observability;

public static class EuroTradeMetrics
{
    public const string MeterName =
        "EuroTrade";

    private static long _outboxPendingMessages;

    public static readonly Meter Meter =
        new(
            MeterName,
            typeof(EuroTradeMetrics)
                .Assembly
                .GetName()
                .Version?
                .ToString());

    public static readonly Counter<long> OrdersCreated =
        Meter.CreateCounter<long>(
            name: "orders_created_total",
            description:
                "Number of successfully created orders.");

    public static readonly Counter<long> OutboxPublishFailures =
        Meter.CreateCounter<long>(
            name: "outbox_publish_failures_total",
            description:
                "Number of failed outbox publishing attempts.");

    public static readonly Counter<long> InboxDuplicateMessages =
        Meter.CreateCounter<long>(
            name: "inbox_duplicate_messages_total",
            description:
                "Number of duplicate inbox messages detected.");

    public static readonly Counter<long> DeadLetteredMessages =
        Meter.CreateCounter<long>(
            name: "dead_lettered_messages_total",
            description:
                "Number of messages explicitly dead-lettered by the application.");

    public static readonly Histogram<double> MessageProcessingDuration =
        Meter.CreateHistogram<double>(
            name: "message_processing_duration",
            unit: "ms",
            description:
                "Service Bus message processing duration.");

    public static readonly ObservableGauge<long> OutboxPendingMessages =
        Meter.CreateObservableGauge(
            name: "outbox_pending_messages",
            observeValue:
                () =>
                    Interlocked.Read(
                        ref _outboxPendingMessages),
            description:
                "Number of unpublished, non-poison outbox messages.");

    public static void SetOutboxPendingMessages(
        long value)
    {
        Interlocked.Exchange(
            ref _outboxPendingMessages,
            Math.Max(
                0,
                value));
    }
}