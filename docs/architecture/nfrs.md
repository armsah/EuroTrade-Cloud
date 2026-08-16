# EuroTrade Cloud — Non-Functional Requirements

## 1. Purpose

This document defines the non-functional requirements (NFRs) for the
EuroTrade Cloud platform.

The requirements establish measurable targets for availability,
performance, security, resilience, deployment, auditability, observability,
and cost.

The targets are intended for a portfolio-scale production reference
architecture and will be validated progressively during implementation.

---

## 2. Availability

### Target

The demonstration environment targets a 99.9% availability objective
for the selected application workload.

The architecture should be capable of supporting a higher availability
target in a production environment through additional redundancy,
automation, and operational controls.

### Evidence

Availability will be demonstrated through:

- Health checks
- Kubernetes readiness and liveness probes
- Failure testing
- Monitoring dashboards
- Documented recovery procedures

### Measurement

Availability will be measured using application and infrastructure
monitoring data.

---

## 3. Performance

### Target

For the committed load-test scenario:

- API P95 latency should be below 400 ms.
- The load profile must be documented.
- Test results must be reproducible.

### Evidence

Performance will be demonstrated through:

- k6 load tests
- Documented test scenarios
- P50 latency
- P95 latency
- P99 latency
- Requests per second
- Error rate

The target applies to the selected API workload and is not interpreted as
a universal latency guarantee for every operation.

---

## 4. Security

The platform must provide:

- Microsoft Entra ID authentication
- Role-based authorization
- Tenant-aware authorization
- Tenant isolation
- Managed identity / Workload Identity
- No application secrets committed to source control
- Secure secret storage using Azure Key Vault where required
- TLS for external communication
- Secure handling of configuration
- Dependency and container security scanning

### Identity Target

Applications should use Azure identity mechanisms rather than long-lived
client secrets.

The preferred Azure credential pattern is:

```text
Application
     |
     v
DefaultAzureCredential
     |
     v
Workload Identity / Managed Identity
     |
     v
Azure Resource
```

### Evidence

Security will be demonstrated through:

- Threat model
- Identity architecture diagram
- Authorization tests
- Tenant-isolation tests
- Dependency security scanning
- Container image scanning
- Software Bill of Materials (SBOM)
- GitHub Actions security controls

---

## 5. Tenant Isolation

Tenant data must be isolated logically and consistently.

Every tenant-scoped business operation must establish the caller's
authorized tenant context before accessing tenant resources.

The system must prevent unauthorized cross-tenant access.

The expected authorization flow is:

```text
Authenticated User
        |
        v
Identity / Claims
        |
        v
Authorized Tenant Context
        |
        v
Application
        |
        v
Tenant-scoped Resource
```

### Evidence

Tenant isolation will be demonstrated through automated authorization
and integration tests.

---

## 6. Resilience

The platform must tolerate common transient failures.

The resilience strategy includes:

- Timeouts
- Controlled retries
- Idempotency
- Durable message processing
- Dead-letter queues
- Transactional outbox
- Inbox / duplicate-processing protection
- Saga compensation
- Health probes

Retries must only be applied to operations that are safe to retry.

Business-critical operations must be idempotent.

---

## 7. Messaging Reliability

Azure Service Bus will provide asynchronous messaging for workflows that
require reliable delivery semantics.

The system must support:

- Retries
- Dead-letter queues
- Idempotent consumers
- Message correlation
- Durable processing state
- Appropriate ordering mechanisms where required

Broker-level duplicate detection may provide additional protection but
does not replace consumer-side idempotency.

---

## 8. Transactional Consistency

The Order Service will use the transactional outbox pattern.

The following operations must be committed within the same database
transaction:

```text
Order State Change
       +
Outbox Message
       |
       v
    COMMIT
```

This prevents a successful business transaction from being committed
without its corresponding publication intent.

---

## 9. Saga Compensation

The order workflow must support compensation when downstream processing
fails.

Example:

```text
Create Order
     |
     v
Reserve Inventory
     |
     v
Capture Payment
     |
     v
Create Shipment
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

Compensating operations must be safe to retry.

---

## 10. Deployment

The target deployment model is designed to support controlled,
low-downtime releases.

The platform will use:

- Containerized .NET services
- Kubernetes
- Helm
- Health probes
- Rolling deployments
- Pod disruption budgets where appropriate
- GitHub Actions
- Azure Container Registry
- Infrastructure as Code

### Evidence

Deployment will be demonstrated through:

- Helm charts
- GitHub Actions workflows
- Successful rolling deployment
- Deployment rollback procedure
- Deployment runbook

---

## 11. Observability

The platform must provide sufficient telemetry to investigate business
and infrastructure failures.

The target observability flow is:

```text
Application
     |
     v
OpenTelemetry
     |
     +--------> Traces
     |
     +--------> Metrics
     |
     +--------> Logs
     |
     v
Azure Monitor / Application Insights
```

Kubernetes metrics may additionally use Managed Prometheus and Grafana
where cost permits.

### Evidence

The project will provide:

- Distributed trace
- Application logs
- Application metrics
- Kubernetes health information
- Monitoring dashboard
- Example order trace crossing asynchronous boundaries

---

## 12. Auditability

Business-critical actions must generate auditable events.

Audit records should include:

- Audit ID
- Tenant ID
- Event type
- Entity type
- Entity ID
- Actor ID
- Timestamp
- Correlation ID
- Relevant event data

Audit records are append-oriented and should not normally be modified as
part of ordinary business processing.

### Evidence

The project will provide:

- Audit component/service
- Audit event definitions
- Audit queries
- Example audit records
- Documentation of audit event flow

---

## 13. Data Protection

Transactional business data will use PostgreSQL.

Documents will use Azure Blob Storage.

Sensitive configuration and secrets will use Azure Key Vault where
required.

The architecture should avoid unnecessary duplication of sensitive data.

### Data Protection Principles

The platform should:

- Encrypt data in transit.
- Use encrypted managed storage services.
- Restrict access using Azure identity and RBAC.
- Avoid storing secrets in source control.
- Avoid logging sensitive credentials or secrets.
- Limit access to tenant data according to authorization rules.

---

## 14. Failure Recovery

The system must have documented responses for important failure
scenarios.

Initial scenarios include:

- Database timeout
- Service restart
- Service Bus failure
- Message processing failure
- Poison message
- Dead-letter queue backlog
- Failed deployment
- Downstream service failure
- Saga compensation

Recovery procedures will be documented as operational runbooks.

### Evidence

The project will provide:

- Failure demonstrations
- Recovery procedures
- DLQ runbook
- Deployment rollback runbook
- Documented compensation flow

---

## 15. Cost

The demonstration environment must be capable of being destroyed when
not required.

Cost documentation will distinguish between:

```text
Demo Environment
        |
        +---- Cost-optimized resources

Production Reference
        |
        +---- Production-grade SKUs
        +---- Higher availability
        +---- Additional networking
        +---- Additional monitoring
```

The project will document:

- Main Azure cost drivers
- Development/demo assumptions
- Production-reference assumptions
- Resources that can be stopped or destroyed
- Estimated monthly cost categories

---

## 16. Maintainability

The codebase must maintain clear architectural boundaries.

The system should:

- Use consistent C# conventions.
- Use explicit service boundaries.
- Keep domain logic independent of infrastructure.
- Maintain automated tests.
- Use ADRs for significant architectural decisions.
- Keep infrastructure reproducible through Terraform.
- Keep deployment configuration version controlled.
- Keep configuration explicit and documented.
- Avoid unnecessary architectural complexity.

---

## 17. Testability

The architecture must support automated testing at multiple levels.

Required test categories include:

- Unit tests
- Integration tests
- Architecture tests
- Contract tests
- End-to-end tests
- Load tests
- Failure tests
- Authorization tests
- Tenant-isolation tests

External dependencies should be replaceable or testable through appropriate
test infrastructure.

### Evidence

The project will provide:

- Automated CI test results
- Test coverage where meaningful
- Integration test results
- Contract test results
- Failure test results
- Load-test results

---

## 18. Scalability

The architecture should allow individual services to scale
independently where workload characteristics justify it.

The target deployment architecture uses:

- Kubernetes deployments
- Separate system and user node pools
- Horizontal scaling where required
- Stateless application instances where practical
- Asynchronous messaging for long-running workflows

Scaling decisions should be based on measured workload characteristics
rather than assumed requirements.

Redis-style caching will only be introduced if performance measurements
demonstrate a justified need.

---

## 19. API Reliability

External APIs must provide predictable behavior.

The API layer should:

- Validate incoming requests.
- Return consistent error responses.
- Enforce authentication and authorization.
- Apply tenant authorization.
- Support appropriate timeout policies.
- Avoid exposing internal implementation details.
- Provide correlation identifiers for troubleshooting.

Business operations that may be retried by clients should support
idempotency where appropriate.

---

## 20. Messaging and Correlation

Business messages must contain sufficient metadata to support reliable
processing and troubleshooting.

Relevant metadata includes:

- Message ID
- Correlation ID
- Tenant ID where appropriate
- Event type
- Occurrence timestamp
- Source service
- Schema/version information where required

Trace and correlation context should be propagated across asynchronous
message boundaries.

---

## 21. Infrastructure Reproducibility

Cloud infrastructure must be reproducible through Infrastructure as Code.

Terraform will be used to define the Azure infrastructure.

The target principle is:

```text
Terraform
    |
    v
Azure Resources
    |
    v
Reproducible Environment
```

Infrastructure changes must be reviewed and version controlled.

The project should support creating the required environment from a clean
starting point using documented Terraform workflows.

---

## 22. Configuration Management

Configuration must be separated from application code where appropriate.

The system should distinguish between:

```text
Application Configuration
        |
        +---- Environment-specific settings

Secrets
        |
        +---- Azure Key Vault / Managed Identity
```

Environment-specific configuration must not require modifying business
logic.

Secrets must never be committed to the Git repository.

---

## 23. Security Scanning

The delivery pipeline should include automated security controls.

These may include:

- Dependency vulnerability scanning
- Container image scanning
- Secret scanning
- Infrastructure security checks
- Static analysis
- Software Bill of Materials generation

Security failures that represent unacceptable risk should prevent a
release from progressing.

---

## 24. Release Reliability

Releases must be reproducible and traceable.

A release should identify:

- Source commit
- Application version
- Container image
- Infrastructure version where applicable
- Deployment configuration
- Release timestamp

The deployment process should support controlled rollback.

---

## 25. NFR Measurement Summary

| Area              | Target                                             | Evidence                                |
| ----------------- | -------------------------------------------------- | --------------------------------------- |
| Availability      | 99.9% demo objective                               | SLO dashboard + failure test            |
| Performance       | P95 < 400 ms for selected load profile             | k6 results                              |
| Security          | Entra ID + RBAC + managed identity                 | Threat model + identity diagram         |
| Tenant isolation  | No unauthorized cross-tenant access                | Authorization tests                     |
| Resilience        | Retry + timeout + idempotency + DLQ + compensation | Failure scenarios                       |
| Messaging         | Reliable asynchronous processing                   | Service Bus + DLQ + idempotency tests   |
| Deployment        | Controlled low-downtime rollout                    | Helm + CI/CD evidence                   |
| Observability     | End-to-end telemetry                               | Trace + metrics + logs                  |
| Audit             | Append-oriented business audit events              | Audit records + queries                 |
| Cost              | Destroyable demo environment                       | Cost documentation                      |
| Maintainability   | Explicit architectural boundaries                  | Architecture tests + code review        |
| Testability       | Automated multi-level testing                      | CI test results                         |
| Scalability       | Independent scaling where justified                | Load testing + deployment configuration |
| Infrastructure    | Reproducible environments                          | Terraform plan/apply                    |
| Security scanning | Automated security checks                          | CI security results                     |
| Release           | Reproducible and traceable releases                | Tagged release + deployment evidence    |

---

## 26. Acceptance Criteria

The NFR phase is considered complete when:

- Availability target is documented.
- Performance target and load profile are documented.
- Security requirements are documented.
- Identity requirements are documented.
- Tenant isolation requirements are documented.
- Resilience requirements are documented.
- Messaging reliability requirements are documented.
- Transactional consistency requirements are documented.
- Saga compensation requirements are documented.
- Deployment requirements are documented.
- Observability requirements are documented.
- Audit requirements are documented.
- Data protection requirements are documented.
- Failure recovery requirements are documented.
- Cost assumptions are documented.
- Maintainability requirements are documented.
- Testability requirements are documented.
- Scalability requirements are documented.
- Infrastructure reproducibility is documented.
- Security scanning requirements are documented.
- Release reliability requirements are documented.
- Measurement evidence is defined for each major requirement.
