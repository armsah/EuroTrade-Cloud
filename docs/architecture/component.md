# EuroTrade Cloud — Component Architecture

## 1. Purpose

This document defines the internal component architecture of the primary
EuroTrade Cloud services.

The component design establishes clear boundaries between:

- API and transport concerns
- Application use cases
- Domain business rules
- Infrastructure and external dependencies

The same architectural principles are applied consistently across the
primary services.

The target structure is:

```text
Service
├── API
├── Application
├── Domain
└── Infrastructure
```

---

## 2. Dependency Direction

The intended dependency direction is:

```text
API
 |
 v
Application
 |
 v
Domain
 ^
 |
Infrastructure
```

The key architectural rule is:

> Domain logic must not depend on infrastructure implementation details.

The responsibilities are:

- **API** handles HTTP requests, responses, authentication context, and
  transport concerns.
- **Application** coordinates business use cases and application workflows.
- **Domain** contains business rules, entities, value objects, and
  invariants.
- **Infrastructure** implements persistence, messaging, and external
  service integrations.

Infrastructure implements abstractions required by the application or
domain boundaries.

---

## 3. Component Architecture Principles

The services follow these principles:

1. Domain logic is independent of infrastructure technologies.
2. Application components coordinate use cases.
3. API components translate external requests into application operations.
4. Infrastructure components implement external dependencies.
5. Business rules belong in the domain layer.
6. Persistence concerns must not leak into domain entities.
7. Messaging concerns must be accessed through explicit abstractions.
8. Tenant context must be available to tenant-scoped application operations.
9. Business-critical operations must be designed for idempotent processing.
10. Each component should have one clear responsibility.
11. External integrations should be replaceable without changing domain
    logic.
12. Services should remain independently testable.

---

## 4. Primary Services

The initial architecture contains four independently deployable services:

```text
EuroTrade Cloud
│
├── Tenant Service
├── Catalog Service
├── Order Service
└── Fulfillment Service
```

These services represent the initial service boundaries established during
the architecture phase.

Additional capabilities are intentionally not separated into independent
services at the beginning:

```text
Billing Simulator
Notification Service
Audit Service
```

These capabilities may initially be implemented within the appropriate
application boundaries or as simpler components.

They should only become independently deployable services when there is a
clear operational, scaling, ownership, or failure-isolation requirement.

This follows the architectural principle:

> Service boundaries should be earned by autonomy and operational need,
> rather than created purely for the sake of using microservices.

---

## 5. Common Service Structure

Each primary service follows the same high-level structure:

```text
Service
│
├── API
│
├── Application
│
├── Domain
│
└── Infrastructure
```

The purpose of each layer is:

### API

Responsible for:

- HTTP endpoints
- Request and response models
- Input validation
- Authentication context
- Authorization entry points
- Mapping HTTP requests to application operations
- Mapping application results to HTTP responses

The API layer must not contain core business rules.

### Application

Responsible for:

- Use cases
- Application commands and queries
- Workflow orchestration
- Transaction coordination
- Calling domain operations
- Calling required infrastructure abstractions

The application layer coordinates work but does not replace domain logic.

### Domain

Responsible for:

- Entities
- Aggregates
- Value objects
- Domain rules
- Domain events
- State transitions
- Business invariants

The domain layer must remain independent of:

- ASP.NET Core
- Entity Framework Core
- Azure SDKs
- Azure Service Bus
- Azure infrastructure

### Infrastructure

Responsible for:

- Database access
- Repository implementations
- Messaging implementations
- External service adapters
- Azure integrations
- Storage implementations
- Infrastructure-specific configuration

Infrastructure implements the abstractions required by the application.

---

# 6. Tenant Service

## Responsibility

The Tenant Service manages tenant metadata and configuration.

Primary responsibilities include:

- Tenant onboarding
- Tenant metadata
- Tenant status
- Tenant plans
- Tenant configuration
- Tenant lifecycle management

The component structure is:

```text
Tenant Service
│
├── API
│   └── TenantEndpoints
│
├── Application
│   ├── CreateTenant
│   ├── GetTenant
│   ├── UpdateTenant
│   └── ConfigureTenant
│
├── Domain
│   ├── Tenant
│   ├── TenantId
│   ├── TenantStatus
│   └── TenantConfiguration
│
└── Infrastructure
    ├── TenantRepository
    └── PostgreSQL
```

The Tenant domain owns the rules governing tenant lifecycle and
configuration.

---

# 7. Catalog Service

## Responsibility

The Catalog Service manages products and pricing information.

Primary responsibilities include:

- Product creation
- Product updates
- Product retrieval
- Product search
- Pricing
- Product status

The component structure is:

```text
Catalog Service
│
├── API
│   └── ProductEndpoints
│
├── Application
│   ├── CreateProduct
│   ├── UpdateProduct
│   ├── GetProduct
│   └── SearchProducts
│
├── Domain
│   ├── Product
│   ├── ProductId
│   ├── Price
│   └── ProductStatus
│
└── Infrastructure
    ├── ProductRepository
    └── PostgreSQL
```

Catalog operations must respect tenant boundaries.

---

# 8. Order Service

The Order Service is the primary business workflow service.

It owns the Order aggregate and order lifecycle.

```text
Order Service
│
├── API
│   ├── OrderEndpoints
│   ├── OrderRequestModels
│   └── OrderResponseModels
│
├── Application
│   ├── CreateOrder
│   ├── ConfirmOrder
│   ├── CancelOrder
│   ├── GetOrder
│   └── ListOrders
│
├── Domain
│   ├── Order
│   ├── OrderId
│   ├── OrderItem
│   ├── OrderStatus
│   ├── Money
│   └── OrderDomainRules
│
└── Infrastructure
    ├── OrderRepository
    ├── OutboxRepository
    ├── InboxRepository
    └── PostgreSQL
```

---

# 9. Order API Components

The Order API exposes operations such as:

```text
POST   /api/orders
GET    /api/orders/{orderId}
GET    /api/orders
POST   /api/orders/{orderId}/confirm
POST   /api/orders/{orderId}/cancel
```

The API layer is responsible for:

- Receiving HTTP requests
- Validating request structure
- Obtaining authentication context
- Obtaining tenant context
- Mapping requests to application operations
- Mapping application results to HTTP responses

The API layer must not directly implement order business rules.

---

# 10. Order Application Components

The Application layer represents order use cases.

Initial use cases include:

```text
CreateOrder
ConfirmOrder
CancelOrder
GetOrder
ListOrders
```

The application flow is:

```text
HTTP Request
     |
     v
API
     |
     v
Application Use Case
     |
     v
Domain
     |
     v
Persistence / Messaging
```

The application layer coordinates the operation while the domain remains
responsible for enforcing business invariants.

---

# 11. Create Order Component

The `CreateOrder` use case performs the following logical sequence:

```text
Client Request
     |
     v
Validate Request
     |
     v
Resolve Tenant Context
     |
     v
Validate Order Data
     |
     v
Create Order Aggregate
     |
     v
Persist Order
     |
     v
Create Outbox Message
     |
     v
Commit Transaction
```

The order and the outbox message must be committed atomically.

---

# 12. Order Domain Components

The Order aggregate represents the business concept of an order.

Conceptually:

```text
Order
│
├── OrderId
├── TenantId
├── CustomerId
├── Items
├── TotalAmount
├── Currency
├── Status
├── CreatedAt
└── Version
```

The domain is responsible for enforcing rules such as:

- An order must belong to a tenant.
- An order must contain at least one item.
- An order cannot be confirmed if it is cancelled.
- An order cannot be modified after completion.
- Order totals must be calculated consistently.
- Invalid state transitions must be rejected.

---

# 13. Order State Machine

The initial order lifecycle is:

```text
Pending
   |
   | Confirm
   v
Confirmed
   |
   | Process
   v
Processing
   |
   | Fulfill
   v
Fulfilled
```

Failure and compensation may result in:

```text
Pending / Confirmed / Processing
             |
             | Cancel / Compensate
             v
        Cancelled
```

The domain layer owns the rules governing valid state transitions.

---

# 14. Fulfillment Service

The Fulfillment Service manages shipment orchestration.

```text
Fulfillment Service
│
├── API
│   └── FulfillmentEndpoints
│
├── Application
│   ├── CreateShipment
│   ├── UpdateShipment
│   ├── CancelShipment
│   └── ProcessFulfillment
│
├── Domain
│   ├── Shipment
│   ├── ShipmentId
│   ├── ShipmentStatus
│   └── FulfillmentRules
│
└── Infrastructure
    ├── ShipmentRepository
    ├── PostgreSQL
    └── ShippingProviderAdapter
```

The initial shipping integration is a simulation.

The shipping provider is represented through an abstraction so that the
implementation can later be replaced without changing the domain model.

---

# 15. Billing Simulator

The Billing Simulator represents a simulated payment-processing boundary.

It supports:

- Payment capture
- Payment failure
- Payment refund
- Payment status

Conceptually:

```text
Billing
│
├── Application
│   ├── CapturePayment
│   └── RefundPayment
│
├── Domain
│   ├── Payment
│   ├── PaymentId
│   └── PaymentStatus
│
└── Infrastructure
    └── SimulatedPaymentProvider
```

No real financial integration is required.

The simulator exists to demonstrate distributed workflow and compensation.

---

# 16. Notification Components

The Notification capability processes notification requests generated by
business events.

```text
Notification
│
├── Application
│   └── SendNotification
│
├── Domain
│   ├── Notification
│   └── NotificationType
│
└── Infrastructure
    └── NotificationProvider
```

The initial notification provider may simulate email or webhook delivery.

---

# 17. Audit Components

The Audit capability consumes business events and creates searchable audit
records.

The flow is:

```text
Business Event
      |
      v
Audit Consumer
      |
      v
Audit Application
      |
      v
Audit Repository
      |
      v
Audit Storage
```

An audit record may contain:

```text
AuditId
TenantId
EventType
EntityType
EntityId
ActorId
OccurredAt
CorrelationId
Data
```

Audit records are append-oriented.

Existing audit records should not normally be modified as part of ordinary
business processing.

---

# 18. Transactional Outbox

The Order Service uses the transactional outbox pattern.

The business state and publication intent are persisted within the same
database transaction.

```text
BEGIN TRANSACTION

    INSERT Order

    INSERT OutboxMessage

COMMIT
```

The resulting architecture is:

```text
Order Service
      |
      +-------------------+
      |                   |
      v                   v
   Order              Outbox
      |                   |
      +---------+---------+
                |
                v
            PostgreSQL
                |
                v
        Outbox Publisher
                |
                v
        Azure Service Bus
```

This ensures that a successful business transaction is not separated from
its corresponding publication intent.

---

# 19. Inbox and Idempotency

Business-critical consumers maintain durable processing state.

The processing model is:

```text
Service Bus Message
        |
        v
Check Inbox / Idempotency Store
        |
        +----------------------+
        |                      |
        | New                  | Already processed
        v                      v
Process message          Ignore safely
        |
        v
Perform business action
        |
        v
Record processed state
```

The consumer must be able to safely handle duplicate delivery.

Service Bus duplicate detection may provide additional protection, but it
does not replace receive-side idempotency.

---

# 20. Messaging Components

Messaging is accessed through explicit abstractions.

Conceptually:

```text
Application
     |
     v
IMessagePublisher
     |
     v
Service Bus Adapter
     |
     v
Azure Service Bus
```

Consumers follow the reverse direction:

```text
Azure Service Bus
     |
     v
Message Handler
     |
     v
Application Use Case
     |
     v
Domain
```

Azure Service Bus-specific implementation details should remain in the
Infrastructure layer.

---

# 21. Tenant Context

Tenant context is a cross-cutting application concern.

The conceptual flow is:

```text
Authenticated Request
        |
        v
Identity / Claims
        |
        v
Tenant Resolution
        |
        v
ITenantContext
        |
        v
Application
        |
        v
Tenant-scoped Domain Operation
```

Tenant context must not be accepted blindly from an untrusted request
parameter.

The authenticated identity and authorization model determine which tenant
the caller is permitted to access.

---

# 22. Authorization

Authentication and authorization are separate concerns.

Authentication determines:

> Who is the caller?

Authorization determines:

> What is the caller allowed to do?

The authorization model is:

```text
Identity
   |
   v
Role / Permission
   |
   v
Tenant
   |
   v
Resource
```

A caller authorized for Tenant A must not be able to access Tenant B
resources without explicit authorization.

Tenant isolation must be enforced at the application and data-access
boundaries.

---

# 23. Correlation and Trace Context

Business operations require correlation and trace context.

The expected flow is:

```text
HTTP Request
     |
     v
Order Service
     |
     v
Outbox Message
     |
     v
Azure Service Bus
     |
     v
Inventory
     |
     v
Billing
     |
     v
Fulfillment
```

Correlation and tracing information must be propagated across HTTP and
asynchronous messaging boundaries.

OpenTelemetry will be used during later implementation phases to provide
distributed tracing.

---

# 24. Error Handling

Errors are categorized according to their nature.

### Validation errors

Examples:

- Invalid order
- Missing required field
- Invalid product

These normally result in a client-visible `4xx` response.

### Authorization errors

Examples:

- Insufficient permission
- Cross-tenant access attempt

These result in an appropriate authorization response.

### Transient infrastructure failures

Examples:

- Database timeout
- HTTP timeout
- Temporary messaging failure

These may be handled using controlled retry and resilience policies.

### Business failures

Examples:

- Insufficient inventory
- Payment declined
- Shipment unavailable

These produce explicit business outcomes and may initiate compensating
actions.

---

# 25. Resilience

HTTP dependencies will use the .NET resilience mechanisms provided by
`Microsoft.Extensions.Http.Resilience`.

The conceptual flow is:

```text
HTTP Request
     |
     v
Timeout
     |
     v
Retry Transient Failures
     |
     v
Circuit Breaker Where Justified
     |
     v
External Dependency
```

Retries must only be applied where the operation is safe to retry.

Business-critical operations must remain idempotent.

---

# 26. Health Components

Each deployed service will expose health endpoints.

The initial convention is:

```text
/health/live
/health/ready
```

### Liveness

Indicates whether the application process is alive.

### Readiness

Indicates whether the application is capable of receiving traffic.

Health endpoints will later be used by Kubernetes probes.

---

# 27. Configuration and Secrets

Application configuration must separate normal configuration from secrets.

The target Azure authentication approach is:

```text
Application
     |
     v
DefaultAzureCredential
     |
     v
Azure Identity
     |
     v
Managed Identity / Workload Identity
```

Secrets must not be committed to Git.

Azure Key Vault will be used where secure secret storage is required.

Applications should avoid long-lived Azure credentials.

---

# 28. Component Interaction — Create Order

The initial order flow is:

```text
Client
  |
  v
Order API
  |
  v
CreateOrder Application Handler
  |
  v
Order Domain
  |
  +---- Validate business rules
  |
  v
Order Repository
  |
  +---- Order
  |
  +---- OutboxMessage
  |
  v
PostgreSQL Transaction
  |
  v
Commit
```

After the transaction commits:

```text
Outbox Publisher
      |
      v
Azure Service Bus
      |
      +--------> Inventory
      |
      +--------> Billing
      |
      +--------> Fulfillment
      |
      +--------> Audit
```

---

# 29. Component Interaction — Failure Compensation

A fulfillment failure can initiate compensation:

```text
Order Created
      |
      v
Inventory Reserved
      |
      v
Payment Captured
      |
      v
Shipment Creation
      |
      X
Failure
      |
      v
Compensation
      |
      +---- Refund Payment
      |
      +---- Release Inventory
      |
      v
Order Cancelled
```

Compensating operations must be safe to retry and must tolerate duplicate
messages.

---

# 30. Target C# Structure

The component architecture will eventually map to a C# solution similar to:

```text
src/
└── EuroTrade.Cloud/
    │
    ├── Tenant/
    │   ├── Api/
    │   ├── Application/
    │   ├── Domain/
    │   └── Infrastructure/
    │
    ├── Catalog/
    │   ├── Api/
    │   ├── Application/
    │   ├── Domain/
    │   └── Infrastructure/
    │
    ├── Order/
    │   ├── Api/
    │   ├── Application/
    │   ├── Domain/
    │   └── Infrastructure/
    │
    └── Fulfillment/
        ├── Api/
        ├── Application/
        ├── Domain/
        └── Infrastructure/
```

The exact C# project and assembly structure will be finalized during P1.

The architecture document defines the logical boundaries; implementation
details may evolve as the system is built and tested.

---

# 31. Testing Boundaries

Each component layer has corresponding testing responsibilities.

```text
Domain
   |
   +---- Unit Tests

Application
   |
   +---- Unit Tests
   +---- Integration Tests

Infrastructure
   |
   +---- Integration Tests

API
   |
   +---- Integration Tests
   +---- End-to-End Tests

Architecture
   |
   +---- Architecture Tests
```

The test strategy will include:

- Domain invariant tests
- Order lifecycle tests
- Tenant isolation tests
- Repository integration tests
- Outbox transaction tests
- Inbox idempotency tests
- Message contract tests
- API authorization tests
- End-to-end order workflow tests
- Saga compensation tests

---

# 32. Architecture Constraints

The following constraints apply to the component architecture:

1. Domain components must not reference ASP.NET Core.
2. Domain components must not reference Entity Framework Core.
3. Domain components must not reference Azure SDKs.
4. Domain components must not reference Azure Service Bus.
5. Application components should depend on abstractions rather than
   infrastructure implementations.
6. Infrastructure implements persistence and external integration
   abstractions.
7. API components must not contain core business-domain logic.
8. Tenant authorization must be enforced before tenant-scoped resource
   access.
9. Business-critical message consumers must be idempotent.
10. Outbox records must be committed with the corresponding business
    transaction.
11. Cross-service communication must use explicit message contracts.
12. Infrastructure concerns must remain replaceable.
13. Services should only be separated when independent deployment,
    scaling, ownership, or failure isolation provides clear value.
14. The system must remain runnable in a local development environment.
15. Cloud-specific implementation details must not leak into the domain
    model.
