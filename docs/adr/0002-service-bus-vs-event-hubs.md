# ADR-0002: Choose Azure Service Bus over Azure Event Hubs

## Status

Accepted

## Date

2026-08-16

## Context

EuroTrade Cloud requires asynchronous communication between services
participating in order and fulfillment workflows.

The platform includes business operations such as:

- Order creation
- Inventory reservation
- Payment simulation
- Shipment orchestration
- Notifications
- Audit events

The architecture must support reliable asynchronous processing and
business workflows involving multiple services.

The messaging platform must support:

- Durable message delivery
- Retries
- Dead-letter queues
- Duplicate handling
- Message settlement
- Business commands and events
- Correlation identifiers
- Tenant context
- Failure handling
- Support for ordered processing where required

Two Azure messaging services were considered:

1. Azure Service Bus
2. Azure Event Hubs

## Decision

EuroTrade Cloud will use **Azure Service Bus** as the primary messaging
platform for business commands and business events.

Service Bus topics and queues will be used to implement asynchronous
communication between the application services.

Event Hubs will not be used as the primary messaging mechanism for the
order and fulfillment workflows.

## Rationale

Azure Service Bus provides messaging semantics that closely match the
business requirements of EuroTrade Cloud.

The platform requires reliable business-message processing rather than
high-volume telemetry ingestion.

Service Bus provides capabilities appropriate for these workflows,
including:

- Queues
- Topics and subscriptions
- Message settlement
- Retries
- Dead-letter queues
- Duplicate detection
- Sessions for ordered processing where required
- Scheduled delivery
- Message properties for application metadata

These capabilities support the Saga, Outbox, and Inbox patterns used by
the platform.

For example:

```text
Order Service
     |
     | OutboxMessage
     v
Outbox Publisher
     |
     v
Azure Service Bus Topic
     |
     +----------------------+
     |                      |
     v                      v
Inventory Service      Fulfillment Service
     |                      |
     v                      v
Reserve Inventory      Create Shipment
```

Service Bus therefore provides a better fit for business workflows where
individual messages represent commands or business events that require
reliable processing and explicit failure handling.

## Event Hubs Consideration

Azure Event Hubs is primarily designed for high-throughput event
streaming and telemetry-style ingestion.

It is well suited to scenarios such as:

- Application telemetry
- Log ingestion
- IoT event streams
- Large-scale streaming analytics
- High-volume event ingestion

These are not the primary messaging requirements for the EuroTrade Cloud
business workflows.

The order and fulfillment workflows require message-oriented processing
semantics rather than primarily stream-oriented ingestion.

Therefore, Event Hubs is not selected for the core business messaging
path.

## Messaging Model

The initial messaging architecture is:

```text
Producer Service
      |
      v
Azure Service Bus Topic
      |
      +-------------------+
      |                   |
      v                   v
Subscription A       Subscription B
      |                   |
      v                   v
Consumer Service     Consumer Service
```

Queues may be used where a single logical consumer should process a
message stream.

Topics and subscriptions will be used where multiple independent
services need to consume the same business event.

## Business Commands and Events

The messaging model distinguishes between commands and events.

### Commands

Commands request an action.

Examples:

```text
ReserveInventory
CapturePayment
CreateShipment
ReleaseInventory
RefundPayment
```

Commands are directed toward a specific consumer or capability.

### Events

Events describe something that has already happened.

Examples:

```text
OrderCreated
InventoryReserved
PaymentCaptured
ShipmentCreated
OrderCancelled
```

Events may be consumed by multiple independent services.

---

## Tenant Context

Tenant-scoped messages must preserve the tenant context required by the
consumer.

Relevant message metadata may include:

```text
MessageId
CorrelationId
TenantId
CausationId
MessageType
```

Example:

```text
OrderCreated
    |
    +-- MessageId
    +-- CorrelationId
    +-- TenantId
    +-- OrderId
```

Consumers must validate tenant ownership before performing operations on
tenant-owned resources.

---

## Reliability and Failure Handling

Service Bus will be used together with application-level reliability
patterns.

The architecture will implement:

- Transactional Outbox
- Consumer Inbox / idempotency state
- Retries
- Dead-letter queues
- Explicit message settlement
- Correlation identifiers
- Idempotent consumers

The broker provides useful duplicate-detection capabilities, but
duplicate detection does not replace receive-side idempotency.

Business-critical consumers must therefore remain idempotent.

Conceptually:

```text
Message
   |
   v
Consumer
   |
   v
Check Inbox / Idempotency State
   |
   +---- Already processed ----> Ignore duplicate
   |
   +---- New message ----------> Execute business operation
                                  |
                                  v
                              Record result
```

---

## Ordering

Some workflows may require ordered processing.

Azure Service Bus sessions may be used where strict message ordering is
required for a logical message group.

The application will avoid assuming global ordering across all messages.

Ordering requirements will be explicitly defined for individual business
workflows.

---

## Dead-Letter Handling

Messages that cannot be successfully processed after the configured
retry policy may be moved to a dead-letter queue.

Example:

```text
Message
   |
   v
Consumer
   |
   v
Processing Failure
   |
   v
Retry
   |
   +---- Success ----> Complete
   |
   +---- Failure ----> Retry
                         |
                         v
                       DLQ
```

Dead-letter messages will be monitored and handled through an operational
runbook.

The project will include a runbook for message backlog and DLQ handling.

---

## Alternatives Considered

### Azure Event Hubs

Advantages:

- Very high-throughput event ingestion.
- Designed for streaming workloads.
- Suitable for telemetry and analytics pipelines.
- Supports partition-based event streams.

Disadvantages for this project:

- Not the primary fit for business command processing.
- Different consumption model from queue/topic business messaging.
- Business workflow failure handling would require additional application
  design.
- Dead-letter and business-message settlement semantics are not the
  primary model.
- Less aligned with the Saga and command-processing requirements.

### Azure Service Bus

Advantages:

- Designed for enterprise messaging.
- Supports queues and topics.
- Supports subscriptions.
- Supports retries and dead-letter queues.
- Supports message settlement.
- Supports sessions for ordered processing.
- Supports duplicate detection.
- Fits command and business-event workflows.
- Integrates naturally with the Outbox and Inbox patterns.

Disadvantages:

- Lower suitability than Event Hubs for extremely high-volume streaming
  ingestion.
- Requires operational configuration of queues, topics, subscriptions,
  retries, and DLQs.
- Messaging costs must be considered for production deployments.

## Consequences

### Positive

- Business workflows have reliable asynchronous messaging.
- Retry and dead-letter behavior can be demonstrated.
- Saga orchestration can use explicit commands and events.
- Multiple services can subscribe independently to business events.
- Tenant and correlation metadata can travel with messages.
- The messaging architecture directly supports the project's failure
  scenarios.

### Negative

- Messaging infrastructure adds operational complexity.
- Developers must understand message delivery and idempotency.
- DLQs require operational monitoring and recovery procedures.
- Consumers must be designed to tolerate duplicate delivery.

## Implementation Notes

The initial local development environment will use a messaging
abstraction so that application services are not tightly coupled to a
specific local broker implementation.

The Azure implementation will use the current Azure Service Bus SDK.

Application code will use explicit message contracts.

Message contracts should contain only the information required by the
consumer and should avoid exposing unnecessary internal implementation
details.

Correlation and tracing information will be propagated through message
application properties.

The transactional Outbox pattern will ensure that business state and
message publication intent are committed consistently.

Consumer-side Inbox/idempotency state will prevent duplicate business
actions.

## Related Architecture

The selected messaging architecture supports the following business flow:

```text
CreateOrder
    |
    v
Order Service
    |
    | Order + OutboxMessage
    v
PostgreSQL Transaction
    |
    v
Outbox Publisher
    |
    v
Azure Service Bus
    |
    +----------------------+
    |                      |
    v                      v
Inventory Service      Billing Simulator
    |                      |
    v                      v
InventoryReserved      PaymentCaptured
    |                      |
    +----------+-----------+
               |
               v
        Fulfillment Service
               |
               v
        Shipment Created
```

Failure handling will support compensating actions.

Example:

```text
Shipment Creation Fails
          |
          v
Compensation
          |
          +----> Refund Payment
          |
          +----> Release Inventory
          |
          v
Order Cancelled
```

## Related Documentation

- `docs/architecture/overview.md`
- `docs/architecture/context.md`
- `docs/architecture/component.md`
- `docs/architecture/nfr.md`
- `docs/architecture/tenant-isolation.md`
- `docs/adr/0001-aks-vs-container-apps.md`
- `docs/adr/0003-postgresql-database.md`
- `docs/adr/0004-tenant-isolation.md`
