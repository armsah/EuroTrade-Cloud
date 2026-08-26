# EuroTrade Cloud

**Production-oriented multi-tenant B2B order platform built with .NET and Azure.**

EuroTrade Cloud is a portfolio cloud-engineering project demonstrating secure, reliable and observable order processing using **.NET, PostgreSQL, Azure Service Bus, AKS, Terraform, Helm and OpenTelemetry**.

The software and infrastructure-as-code demonstrate production-oriented engineering patterns. The actual Azure demo environment is deliberately **cost-optimized** and is not represented as a fully highly available production environment.

---

## 1. What problem is solved?

EuroTrade Cloud demonstrates the technical foundation of a multi-tenant B2B order-processing platform where business operations must remain secure and reliable across database and asynchronous messaging boundaries.

The implemented platform addresses:

- Tenant isolation and tenant-aware authorization
- Order creation and lifecycle management
- PostgreSQL persistence
- Transactional consistency using the **Outbox pattern**
- Duplicate protection using the **Inbox/idempotency pattern**
- Asynchronous processing with Azure Service Bus
- Retry, poison-message and dead-letter handling
- Secure Azure access using Entra ID, RBAC and Workload Identity
- Private connectivity for sensitive Azure services
- Distributed tracing across asynchronous boundaries
- Business and messaging reliability metrics
- Container and Kubernetes runtime security
- Reproducible infrastructure and deployment using Terraform and Helm

The broader architecture is intended to evolve toward **inventory, payment, fulfillment and Saga-based workflows**. Those capabilities are planned and are not presented as completed functionality.

### Capability status

| Capability | Status |
| --- | --- |
| Order domain and API | **Implemented** |
| PostgreSQL persistence | **Implemented** |
| Tenant isolation | **Implemented** |
| Entra ID authentication | **Implemented** |
| Tenant-aware authorization | **Implemented** |
| Transactional Outbox | **Implemented** |
| Durable Inbox / duplicate protection | **Implemented** |
| Idempotent order creation | **Implemented** |
| Azure Service Bus integration | **Implemented** |
| Retry / poison-message handling | **Implemented** |
| Dead-letter handling | **Implemented** |
| OpenTelemetry tracing | **Implemented** |
| Business/reliability metrics | **Implemented** |
| Application Insights / Azure Monitor | **Implemented** |
| Workload Identity / Azure RBAC | **Implemented** |
| Private Azure connectivity | **Implemented in reference architecture** |
| Terraform infrastructure | **Implemented** |
| Helm / Kubernetes deployment | **Implemented** |
| Container/Kubernetes hardening | **Implemented** |
| Inventory | **Planned** |
| Payment | **Planned** |
| Fulfillment | **Planned** |
| Saga orchestration / compensation | **Planned** |
| Multi-node / multi-zone AKS | **Production extension** |
| HPA / cluster autoscaling | **Production extension** |
| PostgreSQL HA | **Production extension** |

This table is the authoritative distinction between **implemented functionality**, **planned business capabilities**, and **production infrastructure extensions**.

---

## 2. What is the architecture?

The application is implemented in **C# / .NET** using explicit **Domain, Application, Infrastructure and API** boundaries.

### Application architecture

```text
Client
  |
  v
ASP.NET Core API
  |
  +--> Authentication / Authorization
  +--> Tenant Context
  |
  v
Application + Domain
  |
  +--------------------------+
  |                          |
  v                          v
PostgreSQL             Transactional Outbox
  |                          |
  +-- Orders                 v
  +-- Idempotency      Outbox Publisher
  +-- Inbox                  |
  +-- Outbox                 v
                       Azure Service Bus
                             |
                             v
                       Message Processor
                             |
                       Inbox / deduplication
                       Dead-letter handling
```

Business state and event-publication intent are committed through the transactional Outbox. The Outbox publisher later sends the event to Azure Service Bus while preserving trace context across the asynchronous boundary.

Consumers use durable Inbox/idempotency state to prevent duplicate delivery from causing duplicate business actions.

### Azure architecture

The reference architecture uses:

- **Compute:** Azure Kubernetes Service
- **Messaging:** Azure Service Bus
- **Database:** Azure Database for PostgreSQL
- **Secrets:** Azure Key Vault
- **Identity:** Microsoft Entra ID + Workload Identity
- **Networking:** VNet, Private Endpoints and Private DNS
- **Registry:** Azure Container Registry
- **Observability:** OpenTelemetry + Application Insights / Azure Monitor
- **Infrastructure:** Terraform
- **Deployment:** Helm / GitHub Actions

The canonical Kubernetes configuration is maintained under:

```text
deploy/helm/eurotrade/
├── Chart.yaml
├── values.yaml
└── templates/
    ├── deployment.yaml
    ├── pdb.yaml
    ├── service.yaml
    └── serviceaccount.yaml
```

Generated Helm manifests and cluster-exported Kubernetes state are not maintained as deployment sources.

---

## 3. What senior engineering problems were solved?

The project focuses on engineering concerns beyond basic CRUD functionality.

### Transactional consistency

Creating database state and publishing an event are two separate operations.

EuroTrade Cloud addresses this dual-write problem with a **transactional Outbox**, ensuring that committed business changes have a corresponding durable publication intent.

### Idempotency and duplicate processing

At-least-once delivery means duplicate operations must be expected rather than treated as exceptional.

The project uses:

- Durable Inbox state
- Idempotent order creation
- Duplicate detection
- PostgreSQL concurrency protection

Critical concurrency behavior is tested against PostgreSQL using **Testcontainers** rather than inferred from SQLite behavior.

### Failure handling

Implemented reliability behavior includes:

- Outbox retries
- Retry scheduling
- Poison-message handling
- Dead-letter handling
- Transaction rollback verification
- Cancellation propagation
- Concurrent-processing protection

Cross-service Saga compensation remains a **planned business milestone**.

### Multi-tenancy and authorization

Tenant identity is propagated through the authenticated application context.

Order access is tenant-aware, with authorization enforced at the API/application boundary to prevent one tenant from accessing another tenant's resources.

The implementation distinguishes authentication, scopes/permissions and tenant ownership rather than treating them as a single security check.

### Cloud identity and least privilege

Azure workloads use:

- Microsoft Entra ID
- Azure RBAC
- Workload Identity
- Azure Key Vault
- Private connectivity

The application avoids embedding Azure credentials and does not require unnecessary Azure management privileges simply to expose infrastructure telemetry.

### Container and Kubernetes security

The application container explicitly runs as a **non-root user**.

The Kubernetes deployment applies runtime restrictions including:

```yaml
runAsNonRoot: true

allowPrivilegeEscalation: false
readOnlyRootFilesystem: true

capabilities:
  drop:
    - ALL

seccompProfile:
  type: RuntimeDefault
```

This reduces privileges at both the Azure identity and Linux container-runtime layers.

### Observability across asynchronous boundaries

Trace context is preserved across the transactional Outbox boundary:

```text
HTTP Request
     |
     v
Create Order
     |
     +--> Order + Outbox committed
                    |
                    | trace context stored
                    v
              Outbox Publisher
                    |
                    | trace context restored
                    v
             Azure Service Bus
                    |
                    v
             Message Processor
```

The application also exposes OpenTelemetry business and reliability metrics:

| Metric | Purpose |
| --- | --- |
| `orders_created_total` | Successfully created orders |
| `outbox_pending_messages` | Current unpublished Outbox backlog |
| `outbox_publish_failures_total` | Failed publication attempts |
| `message_processing_duration` | Message-processing duration |
| `inbox_duplicate_messages_total` | Duplicate messages detected |
| `dead_lettered_messages_total` | Messages dead-lettered by application processing |

Azure Service Bus / Azure Monitor remains responsible for infrastructure-owned telemetry such as **queue depth, DLQ depth, broker errors and throttling**.

Common OpenTelemetry resource attributes include:

- `service.name`
- `service.version`
- `deployment.environment.name`

This allows traces, logs and metrics to be correlated by service, application version and environment.

### Testing failure paths

The solution includes:

- Domain unit tests
- Application unit tests
- Architecture tests
- Integration tests
- PostgreSQL/Testcontainers tests
- End-to-end API tests

Coverage includes authorization boundaries, tenant isolation, idempotency, concurrency, Inbox behavior, Outbox retries, rollback, cancellation and poison-message handling.

The objective is to test not only successful execution but also the failure modes expected in a distributed system.

---

## 4. How can I run or inspect the demo?

### Prerequisites

- .NET 10 SDK
- Git
- Docker Desktop
- Helm for Kubernetes chart validation
- Azure CLI when testing Azure integrations

### Clone

```bash
git clone https://github.com/armsah/EuroTrade-Cloud.git
cd EuroTrade-Cloud
```

### Build

```bash
dotnet build
```

### Run the tests

```bash
dotnet test
```

The automated test suite exercises domain, application, architecture, PostgreSQL integration and end-to-end API behavior.

### Inspect the Kubernetes deployment

Validate the Helm chart:

```bash
helm lint ./deploy/helm/eurotrade
```

Render the Kubernetes configuration locally:

```bash
helm template eurotrade ./deploy/helm/eurotrade
```

### Inspect the Azure implementation

The Azure environment is defined through Terraform and Helm.

When provisioned, the reference environment includes:

- AKS
- Azure Service Bus
- Azure Database for PostgreSQL
- Azure Key Vault
- Azure Container Registry
- VNet / Private Endpoints / Private DNS
- Application Insights
- Azure Monitor / Log Analytics

Azure resources are **not intended to remain running continuously for the portfolio demo**. The environment can be provisioned for demonstration and destroyed afterward to control recurring cloud costs.

---

## Availability and production topology

The provisioned Azure environment is intentionally a **cost-optimized development/demo configuration**, not a claim of infrastructure-level high availability.

The current workload demonstrates:

- Multiple API replicas
- Liveness and readiness probes
- CPU and memory requests/limits
- Rolling deployments
- PodDisruptionBudget
- Stateless API containers
- Durable messaging patterns

Multiple replicas do not by themselves guarantee high availability if they run on the same AKS node.

A continuously operated production deployment would typically add:

- Multiple AKS worker nodes
- Availability-zone distribution where required
- Cluster Autoscaler
- Horizontal Pod Autoscaler
- Topology spread constraints or pod anti-affinity
- PostgreSQL HA and tested recovery
- Capacity and disruption testing
- Operational SLOs and alerting

These resources are intentionally not provisioned solely for the portfolio demonstration.

---

## Roadmap

The next business milestone is to extend the reliable order-processing foundation with:

1. Inventory reservation
2. Payment processing/simulation
3. Fulfillment workflow
4. Explicit Saga state
5. Compensation behavior
6. Workflow-specific failure tests
7. Cross-service observability

These capabilities are **planned and are not represented as completed functionality**.

Production infrastructure improvements such as multi-zone AKS, autoscaling and PostgreSQL HA are separate from the cost-optimized demo environment.

---

## Engineering focus

EuroTrade Cloud is intended to demonstrate engineering judgment rather than simply the number of technologies used.

The main areas are:

- **Application engineering:** domain-oriented .NET design and explicit boundaries
- **Distributed systems:** Outbox, Inbox, idempotency, retries and dead-letter handling
- **Data consistency:** PostgreSQL transactions, concurrency and rollback
- **Security:** Entra ID, tenant authorization, RBAC, Workload Identity and hardened containers
- **Cloud engineering:** AKS, Service Bus, PostgreSQL, Terraform and Helm
- **Observability:** OpenTelemetry traces, logs and metrics
- **Testing:** unit, architecture, integration, PostgreSQL and end-to-end testing

The repository deliberately distinguishes between **what is implemented today**, **what is planned next**, and **what would be added for a continuously operated production environment**.
