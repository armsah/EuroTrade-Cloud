# ADR-0003: Choose PostgreSQL for Transactional Data

## Status

Accepted

## Date

2026-08-16

## Context

EuroTrade Cloud requires a transactional database for core business data.

The platform must persist:

- Tenants
- Customers
- Products
- Orders
- Order items
- Inventory reservations
- Payment simulation state
- Fulfillment state
- Outbox messages
- Inbox / idempotency state
- Audit metadata

The database must support:

- ACID transactions
- Strong consistency
- Relational data modeling
- Referential integrity
- Concurrent transactions
- Reliable persistence
- Entity Framework Core integration
- Database migrations
- Local development
- Azure deployment
- Cost-conscious portfolio development

The Order Service also requires the transactional Outbox pattern.

The Order entity and its corresponding OutboxMessage must be committed
atomically.

Conceptually:

```text
Begin Transaction
      |
      +---- Create / Update Order
      |
      +---- Create Outbox Message
      |
      v
Commit Transaction
      |
      v
Both changes become durable together
```

## Decision

EuroTrade Cloud will use **PostgreSQL** as the primary relational
transactional database.

The production-reference architecture will use **Azure Database for
PostgreSQL**.

Entity Framework Core will be used for application persistence.

## Rationale

PostgreSQL provides the relational and transactional capabilities
required by the platform.

It is well suited to:

- Order management
- Catalog data
- Tenant metadata
- Inventory state
- Transactional workflows
- Outbox persistence
- Inbox / idempotency state
- Relational reporting queries

PostgreSQL also provides strong support for constraints, indexes,
transactions, concurrency control, and structured relational data.

The technology is mature, widely adopted, and suitable for both local
development and managed Azure deployment.

## Transactional Consistency

The Order Service will use a database transaction when updating business
state and creating an OutboxMessage.

Example:

```text
Order Service
     |
     v
Begin PostgreSQL Transaction
     |
     +---- Insert Order
     |
     +---- Insert OutboxMessage
     |
     v
Commit
```

If the transaction fails:

```text
Order Insert       -> Rolled back
Outbox Insert      -> Rolled back
```

If the transaction succeeds:

```text
Order Insert       -> Committed
Outbox Insert      -> Committed
```

This prevents the application from reaching a state where an order is
committed but the intent to publish its business event has been lost.

## Tenant Isolation

EuroTrade Cloud initially uses logical tenant isolation within the
shared PostgreSQL deployment.

Tenant-owned records contain tenant ownership information.

Example:

```text
Orders
--------------------------------
Id
TenantId
CustomerId
Status
CreatedAt
```

Tenant-scoped queries must apply the authorized tenant context.

Conceptually:

```sql
SELECT *
FROM Orders
WHERE Id = @OrderId
  AND TenantId = @AuthorizedTenantId;
```

The application must not rely on a client-supplied `TenantId` as proof of
authorization.

Tenant authorization is documented separately in:

```text
docs/architecture/tenant-isolation.md
docs/adr/0004-tenant-isolation.md
```

## Entity Framework Core

Entity Framework Core will be used as the primary data-access technology.

The application will use:

- Entity configurations
- Explicit relationships
- Database constraints
- Indexes
- Migrations
- Transactions where required
- Parameterized queries

Database schema changes will be managed through controlled EF Core
migration processes.

Production migrations must be treated as a deployment concern rather
than automatically executing unrestricted schema changes during
application startup.

## Database Schema Principles

The initial schema will favor clear relational modeling.

Core entities will include ownership and lifecycle information where
required.

Examples:

```text
Tenant
Customer
Product
Order
OrderItem
InventoryReservation
Payment
Shipment
OutboxMessage
InboxMessage
AuditRecord
```

Relationships and foreign keys will be explicitly defined.

Business invariants should be enforced at the appropriate application
and database boundaries.

## Indexing

Indexes will be introduced based on query patterns and measured
performance requirements.

Expected examples include indexes involving:

```text
TenantId
OrderId
CustomerId
ProductId
Status
CreatedAt
```

Composite indexes may be introduced where tenant-scoped query patterns
require them.

Indexes will be validated against actual query performance rather than
added indiscriminately.

## Outbox Persistence

The transactional Outbox pattern requires persistent storage for
messages that have been committed together with business state.

Example:

```text
OutboxMessage
--------------------------------
Id
MessageType
AggregateId
TenantId
CorrelationId
Payload
CreatedAt
PublishedAt
RetryCount
```

The Outbox Publisher will later read unpublished messages and publish
them to Azure Service Bus.

This separates business transaction consistency from message delivery.

## Inbox / Idempotency Persistence

Business-critical consumers will maintain durable state to prevent
duplicate business actions.

Conceptually:

```text
InboxMessage
--------------------------------
MessageId
Consumer
TenantId
ProcessedAt
Result
```

A consumer will check whether a message has already been processed before
executing the corresponding business operation.

This supports idempotent message processing.

## High Availability

The production-reference architecture should use the availability
features provided by Azure Database for PostgreSQL.

The portfolio demonstration environment may use a smaller and
cost-conscious configuration.

The exact production SKU and availability configuration will be
documented separately in the project's cost and infrastructure
documentation.

The architecture must distinguish between:

```text
Demo Environment
        |
        v
Cost-optimized configuration

Production Reference
        |
        v
Higher availability and resilience configuration
```

## Backup and Recovery

Production deployments must use managed database backup and recovery
capabilities.

Recovery objectives will be documented as part of the operational
design.

The portfolio environment should remain disposable and reproducible
through infrastructure-as-code.

## Alternatives Considered

### Azure SQL Database

Advantages:

- Fully managed Azure relational database.
- Strong integration with the Microsoft ecosystem.
- Mature enterprise capabilities.
- Excellent .NET and Entity Framework Core support.

Disadvantages for this project:

- PostgreSQL provides a strong open-source relational option.
- The project benefits from demonstrating portability across common
  relational database technologies.
- PostgreSQL is well suited to the project's transactional workload.

Azure SQL remains a viable alternative for future implementations.

### Cosmos DB

Advantages:

- Globally distributed NoSQL database.
- Flexible document model.
- Horizontal scalability.
- Low-latency access patterns for suitable workloads.

Disadvantages for this project:

- The core business model is strongly relational.
- Orders, customers, products, inventory, and tenant relationships
  benefit from relational constraints and transactions.
- Introducing a distributed NoSQL model would add complexity without a
  demonstrated requirement.

Cosmos DB is therefore not selected for the primary transactional store.

### In-Memory or Local Database

Advantages:

- Very simple local development.
- Minimal infrastructure requirements.

Disadvantages:

- Not appropriate as the production transactional store.
- Does not provide the required persistence characteristics.
- Would not demonstrate realistic production database behavior.

An in-memory database may still be used selectively for unit-level
testing, but not as the application's system of record.

## Consequences

### Positive

- Strong transactional consistency.
- Clear relational data model.
- Supports the transactional Outbox pattern.
- Good Entity Framework Core integration.
- Suitable for local development.
- Suitable for Azure managed deployment.
- Supports tenant-scoped relational queries.
- Mature indexing and constraint capabilities.

### Negative

- Database schema changes require migration discipline.
- Scaling relational workloads requires capacity planning.
- Database availability and backup configuration affect operational
  complexity.
- Poorly designed queries or indexes can negatively affect performance.
- Shared-database tenant isolation requires careful application-level
  enforcement.

## Performance

The platform's initial performance target is:

```text
API P95 latency < 400 ms
```

for the committed load-test scenario.

Database performance will be evaluated through:

- Query execution time
- Index effectiveness
- Connection pool behavior
- Transaction duration
- Lock contention
- Database CPU and memory
- Application-level latency

Performance optimizations will be based on measurements rather than
premature infrastructure complexity.

## Security

Database credentials and connection information must not be committed
to source control.

Production Azure authentication should use Azure identity mechanisms
where supported.

Secrets required by the application will be stored using Azure Key Vault
and accessed using managed identity / Workload Identity patterns.

Database access must follow least-privilege principles.

## Local Development

Local development will use PostgreSQL through a reproducible development
environment.

The application should be able to run against a local PostgreSQL
instance without requiring Azure resources.

This supports:

- Local development
- Integration testing
- CI testing
- Faster feedback
- Cost control

## Testing

Database behavior will be tested progressively.

Required tests include:

- Entity persistence
- Transaction behavior
- Database constraints
- EF Core mappings
- Migration validation
- Outbox persistence
- Inbox / idempotency persistence
- Tenant isolation
- Integration workflows

Integration tests will use PostgreSQL-compatible test infrastructure,
with Testcontainers planned for realistic integration testing.

## Consequences for Infrastructure

The infrastructure layer will eventually provision Azure Database for
PostgreSQL through Terraform.

The infrastructure structure is:

```text
infra/
├── bootstrap/
├── modules/
└── environments/
```

The database infrastructure will be separated from application code and
managed as infrastructure-as-code.

## Related Documentation

- `docs/architecture/overview.md`
- `docs/architecture/context.md`
- `docs/architecture/component.md`
- `docs/architecture/nfr.md`
- `docs/architecture/tenant-isolation.md`
- `docs/adr/0001-aks-vs-container-apps.md`
- `docs/adr/0002-service-bus-vs-event-hubs.md`
- `docs/adr/0004-tenant-isolation.md`
