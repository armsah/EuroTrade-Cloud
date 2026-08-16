# ADR-0004: Choose Logical Tenant Isolation in a Shared PostgreSQL Database

## Status

Accepted

## Date

2026-08-16

## Context

EuroTrade Cloud is a multi-tenant B2B order and fulfillment platform.

Multiple business customers use the same application platform, while
their business data must remain isolated.

The platform must prevent one tenant from accessing another tenant's:

- Customers
- Products
- Orders
- Inventory
- Payments
- Shipments
- Documents
- Audit records

Tenant isolation must apply consistently across synchronous APIs,
database access, asynchronous messaging, background processing, document
storage, and caching.

Several tenant-isolation models were considered:

1. Logical isolation within a shared database.
2. Schema-per-tenant.
3. Database-per-tenant.

The initial portfolio implementation must balance security, operational
simplicity, scalability, development effort, and demonstration cost.

## Decision

EuroTrade Cloud will initially use **logical tenant isolation within a
shared PostgreSQL database**.

Tenant-owned records will contain a `TenantId` or equivalent tenant
ownership reference.

Application authorization and tenant-scoped data-access rules will
enforce tenant boundaries.

The application must establish an authorized tenant context before
accessing tenant-owned resources.

The architecture will use defense in depth where appropriate, including
the potential evaluation of PostgreSQL Row-Level Security.

## Tenant Context

The conceptual flow is:

```text
Authenticated Principal
        |
        v
Identity / Claims
        |
        v
Tenant Authorization
        |
        v
Authorized Tenant Context
        |
        v
Application Service
        |
        v
Tenant-scoped Data Access
        |
        v
PostgreSQL
```

A client-supplied `TenantId` is not sufficient proof of authorization.

The server must determine whether the authenticated principal is
authorized to operate within the requested tenant.

## Database Model

Tenant-owned entities will contain tenant ownership information.

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

Tenant-scoped data access must apply the authorized tenant constraint.

Conceptually:

```sql
SELECT *
FROM Orders
WHERE Id = @OrderId
  AND TenantId = @AuthorizedTenantId;
```

The application must fail closed when a valid tenant context is missing
or invalid.

## Authorization

Microsoft Entra ID will provide authentication for supported users and
application identities.

ASP.NET Core authorization policies will enforce application-level
authorization.

Tenant authorization will be evaluated before tenant-owned business data
is returned or modified.

Normal tenant users will only have access to their authorized tenant.

Privileged administrative access will require explicit roles and
authorization policies.

Administrative operations must be auditable.

## Messaging

Tenant context must be preserved across asynchronous workflows.

Business messages may contain metadata such as:

```text
MessageId
CorrelationId
TenantId
CausationId
MessageType
```

Consumers must validate that referenced resources belong to the expected
tenant before performing business operations.

Tenant isolation must therefore continue across:

```text
Order Service
      |
      v
Azure Service Bus
      |
      v
Inventory / Billing / Fulfillment
```

Message processing must also be idempotent.

## Document Storage

Tenant-owned documents must be associated with their owning tenant.

Document access must validate:

```text
Authenticated Principal
        +
Authorized Tenant
        +
Document Ownership
        |
        v
Document Access
```

A document identifier alone must never be treated as proof of
authorization.

## Caching

A distributed cache will only be introduced if performance measurements
demonstrate a justified need.

If tenant-scoped caching is introduced, tenant identity must form part of
the cache key.

Example:

```text
tenant:{tenantId}:product:{productId}
```

Cache entries must never be shared between tenants unintentionally.

## Alternatives Considered

### Schema-per-Tenant

Each tenant would receive a separate database schema.

Advantages:

- Stronger logical separation than a single shared schema.
- Tenant-specific schema boundaries.
- Potentially easier tenant-level backup or migration strategies.

Disadvantages:

- Operational complexity increases with the number of tenants.
- Schema migrations become more complicated.
- Connection and schema management become more complex.
- The approach is unnecessary for the initial portfolio-scale
  implementation.

Schema-per-tenant may be considered if future requirements justify the
additional complexity.

### Database-per-Tenant

Each tenant would receive an independent database.

Advantages:

- Strongest isolation of the considered models.
- Tenant-specific backup and recovery.
- Easier separation for highly sensitive or regulated workloads.

Disadvantages:

- Significantly greater operational complexity.
- Higher infrastructure cost.
- Database provisioning and lifecycle management become more difficult.
- Schema migrations must be coordinated across many databases.
- Not appropriate for the initial demonstration environment.

Database-per-tenant may be appropriate for specific enterprise or
regulatory scenarios in a future architecture.

### Shared Database with Logical Isolation

Advantages:

- Lowest operational complexity.
- Cost-efficient for the portfolio environment.
- Simple infrastructure model.
- Straightforward tenant-aware relational queries.
- Easy local development.
- Supports the initial business requirements.

Disadvantages:

- Requires disciplined application-level authorization.
- A query that fails to apply tenant constraints could expose data.
- Stronger defense-in-depth mechanisms may be required for higher-risk
  production environments.

This is the selected approach.

## Security Controls

Tenant isolation will be enforced through multiple layers:

```text
Microsoft Entra ID
        |
        v
ASP.NET Core Authorization
        |
        v
Authorized Tenant Context
        |
        v
Tenant-scoped Application Services
        |
        v
Tenant-scoped Data Access
        |
        v
PostgreSQL
```

Additional controls include:

- Server-side authorization
- Tenant ownership checks
- Fail-closed behavior
- Least privilege
- Automated cross-tenant tests
- Audit logging for privileged operations
- Secure document access
- Tenant-aware message processing

## Failure Scenarios

The implementation must explicitly test the following scenarios.

### Cross-Tenant API Request

Tenant A attempts to access an order belonging to Tenant B.

Expected result:

```text
Access denied
No Tenant B data returned
```

### Modified Tenant ID

A client modifies a request's `TenantId`.

Expected result:

```text
Client input does not bypass authorization
Request rejected if tenant authorization fails
```

### Cross-Tenant Message

A message references a resource belonging to another tenant.

Expected result:

```text
Consumer validates ownership
Business operation rejected
Failure handled according to messaging policy
```

### Cross-Tenant Document

A Tenant A user attempts to retrieve a Tenant B document.

Expected result:

```text
Authorization fails
Document contents are not returned
```

### Missing Tenant Context

A tenant-scoped operation has no valid tenant context.

Expected result:

```text
Operation rejected
No tenant-owned data accessed
```

## Testing Strategy

Tenant isolation will be demonstrated through automated tests.

Required scenarios include:

- Tenant A can access Tenant A data.
- Tenant B can access Tenant B data.
- Tenant A cannot access Tenant B data.
- Tenant B cannot access Tenant A data.
- Invalid tenant context is rejected.
- Modified tenant identifiers cannot bypass authorization.
- Cross-tenant messages are rejected.
- Cross-tenant documents cannot be accessed.
- Missing tenant context is rejected.
- Administrative access follows separate authorization rules.

Tests will be implemented across:

```text
tests/
├── unit/
├── integration/
├── architecture/
└── e2e/
```

## Consequences

### Positive

- Cost-effective initial architecture.
- Simple local development model.
- Straightforward relational data model.
- Low operational overhead.
- Supports the portfolio's multi-tenant requirements.
- Compatible with Azure Database for PostgreSQL.
- Provides a clear path toward stronger isolation if requirements change.

### Negative

- Tenant isolation requires strict application discipline.
- Every tenant-scoped query must enforce ownership.
- Cross-tenant access must be explicitly tested.
- Shared infrastructure creates a larger blast radius if an isolation
  control fails.
- Stronger isolation models may be required for specific regulatory or
  enterprise customers.

## Future Evolution

The isolation model may be revisited if business requirements change.

Potential future options include:

```text
Shared Database
      |
      +----> Schema-per-Tenant
      |
      +----> Database-per-Tenant
```

Possible triggers for revisiting the decision include:

- Regulatory requirements
- Customer-specific isolation requirements
- Data residency requirements
- Contractual security requirements
- Tenant-specific backup and recovery requirements
- Significant differences in tenant workload
- Demonstrated limitations of shared logical isolation

Any change to the isolation model must be documented through a new or
superseding ADR.

## Related Documentation

- `docs/architecture/tenant-isolation.md`
- `docs/architecture/nfr.md`
- `docs/architecture/overview.md`
- `docs/adr/0003-postgresql-database.md`
- `docs/adr/0002-service-bus-vs-event-hubs.md`
