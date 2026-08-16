# EuroTrade Cloud — Tenant Isolation Strategy

## 1. Purpose

EuroTrade Cloud is a multi-tenant B2B order and fulfillment platform.

Multiple business customers share the same application platform, while
their business data must remain logically isolated.

The tenant-isolation strategy ensures that an authenticated user or
application can access only resources belonging to a tenant for which it
is authorized.

Tenant isolation applies to:

- APIs
- Database data
- Messaging
- Background processing
- Documents
- Audit records
- Caching

---

## 2. Tenant Isolation Requirements

The platform must:

1. Establish an authorized tenant context for every tenant-scoped request.
2. Prevent clients from selecting an arbitrary tenant and bypassing authorization.
3. Enforce tenant authorization on the server side.
4. Apply tenant constraints to database access.
5. Preserve tenant context during asynchronous processing.
6. Prevent cross-tenant document access.
7. Preserve tenant ownership in audit records.
8. Apply explicit authorization to privileged administrative access.
9. Provide automated tests proving that cross-tenant access is rejected.

---

## 3. Authentication and Authorization

Microsoft Entra ID provides authentication for supported users and
application identities.

ASP.NET Core authorization policies determine what an authenticated
principal is allowed to access.

Authentication answers:

```text
Who is the caller?
```

Authorization answers:

```text
What is the caller allowed to access?
```

Tenant authorization must be evaluated before tenant-scoped business data
is accessed.

The application must not rely solely on a tenant identifier supplied by
the client.

---

## 4. Tenant Context

The expected tenant-context flow is:

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
```

The application establishes the authorized tenant context from trusted
identity and authorization information.

A client-supplied `TenantId` is not sufficient proof of authorization.

The server must verify that the authenticated principal is authorized to
operate within the requested tenant.

---

## 5. Request Flow

Tenant-scoped API requests follow this conceptual flow:

```text
Client
  |
  v
API Edge
  |
  v
Authentication
  |
  v
Authorization
  |
  v
Tenant Context
  |
  v
Application Service
  |
  v
Repository / Data Access
  |
  v
Tenant-scoped Query
  |
  v
PostgreSQL
```

Tenant authorization must occur before tenant-owned business data is
returned or modified.

---

## 6. Database Isolation

The initial implementation uses logical tenant isolation within a shared
PostgreSQL deployment.

Tenant-owned entities contain tenant ownership information.

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

Tenant-scoped queries must include the authorized tenant context.

Conceptually:

```sql
WHERE Id = @OrderId
AND TenantId = @AuthorizedTenantId
```

The application must never rely solely on a client-provided tenant ID.

PostgreSQL Row-Level Security may be evaluated as an additional
defense-in-depth mechanism during implementation if it provides
sufficient security value without unnecessary operational complexity.

---

## 7. Data Access Rules

Tenant-scoped repositories must require an authorized tenant context when
accessing tenant-owned entities.

Tenant-owned entities must contain tenant ownership information or an
equivalent ownership representation.

Data-access operations must consistently enforce the authorized tenant
constraint.

Unrestricted access to tenant-owned data must not be exposed to normal
application services.

The application should fail closed when a valid tenant context is not
available.

---

## 8. Messaging and Background Processing

Tenant context must be preserved for tenant-scoped business messages.

Example:

```text
Order Service
     |
     | OrderCreated
     | TenantId
     | CorrelationId
     v
Azure Service Bus
     |
     v
Inventory Service
     |
     v
Tenant-scoped Processing
```

Messages that operate on tenant-owned resources must contain sufficient
context to identify the owning tenant.

Consumers must validate that referenced resources belong to the
appropriate tenant before performing business actions.

Message processing must also be idempotent to prevent duplicate business
operations.

---

## 9. Document Storage

Tenant-owned documents must be associated with their owning tenant.

Document access must verify:

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

A user authorized for Tenant A must not be able to retrieve a document
belonging to Tenant B.

Document identifiers alone must not be treated as proof of authorization.

---

## 10. Caching

A distributed cache such as Redis will only be introduced if performance
measurements demonstrate a justified need.

If tenant-scoped caching is introduced, cache keys must include tenant
identity.

Example:

```text
tenant:{tenantId}:product:{productId}
```

Tenant-scoped cache entries must never be reusable across tenants.

---

## 11. Administrative Access

Platform administrators may require controlled cross-tenant visibility
for operational purposes.

Administrative access must:

- Require an explicitly assigned administrative role.
- Use server-side authorization policies.
- Follow least-privilege principles.
- Be auditable.
- Be distinguishable from normal tenant-user activity.

Normal tenant users must remain restricted to their authorized tenant.

---

## 12. Cross-Tenant Failure Scenarios

### Scenario 1 — Wrong Tenant

A user authenticated for Tenant A requests an order belonging to Tenant B.

Expected result:

- Access is denied.
- No Tenant B data is returned.
- The operation is observable through appropriate telemetry.

### Scenario 2 — Modified Tenant ID

A client modifies the `TenantId` in an HTTP request.

Expected result:

- Client input does not bypass authorization.
- The server validates tenant membership.
- The request is rejected if authorization fails.

### Scenario 3 — Cross-Tenant Message

A message references a resource belonging to another tenant.

Expected result:

- The consumer validates resource ownership.
- The business operation is rejected.
- The failure is handled according to messaging and DLQ policy.

### Scenario 4 — Cross-Tenant Document

A user attempts to retrieve another tenant's document.

Expected result:

- Authorization fails.
- Document contents are not returned.

### Scenario 5 — Missing Tenant Context

A tenant-scoped operation is executed without a valid tenant context.

Expected result:

- The operation is rejected.
- No tenant-owned data is accessed.

---

## 13. Testing Strategy

Tenant isolation must be verified through automated tests.

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

Testing will be implemented progressively across:

```text
tests/
├── unit/
├── integration/
├── architecture/
└── e2e/
```

---

## 14. Observability and Audit

Tenant context should be available in application telemetry where
appropriate.

Relevant telemetry may include:

- Tenant ID
- User or application identity
- Correlation ID
- Trace ID
- Operation name

Sensitive information must not be written to logs.

Tenant-scoped business operations should generate appropriate audit
events.

Potential cross-tenant authorization failures should be observable
through application security telemetry.

---

## 15. Security Principles

The tenant-isolation design follows these principles:

- Deny by default.
- Authorize on the server.
- Never trust client-supplied tenant identifiers.
- Enforce tenant ownership at data-access boundaries.
- Preserve tenant context across asynchronous workflows.
- Apply least privilege.
- Fail closed when tenant context is missing or invalid.
- Test isolation explicitly.
- Audit privileged operations.
- Use defense in depth.

---

## 16. Initial Architecture Decision

The initial EuroTrade Cloud implementation will use logical tenant
isolation within a shared PostgreSQL deployment.

Tenant ownership will be enforced through:

- Microsoft Entra ID authentication
- Application authorization policies
- Authorized tenant context
- Tenant-scoped data access
- Tenant-aware messaging
- Tenant-aware document access
- Automated cross-tenant tests

This approach provides a practical balance between security, operational
simplicity, scalability, and demonstration cost.

Alternative isolation models, such as schema-per-tenant or
database-per-tenant, may be evaluated if future business or regulatory
requirements require stronger physical isolation.

The architectural rationale for the selected strategy will be recorded
in an Architecture Decision Record (ADR).
