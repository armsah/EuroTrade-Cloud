# EuroTrade Cloud

**Production-oriented multi-tenant B2B order platform for European companies.**

## 1. What problem is solved?

EuroTrade Cloud demonstrates a secure, resilient and observable B2B order-processing platform designed for multiple business tenants.

The platform addresses:

* Tenant isolation and tenant-aware authorization
* Order creation and lifecycle management
* Asynchronous order processing
* Inventory, payment and fulfillment orchestration
* Reliable event delivery with retries and idempotency
* Transactional consistency using the **Outbox pattern**
* Duplicate protection using the **Inbox/idempotency pattern**
* Secure Azure access using Entra ID, RBAC and Workload Identity
* Private connectivity for sensitive Azure services
* Distributed observability with OpenTelemetry and Application Insights

The project is intentionally designed as a **portfolio-grade production reference architecture**. The deployed Azure environment uses a cost-optimized development/demo configuration that can be recreated and destroyed to control cloud costs, while the infrastructure and application design demonstrate patterns that can be extended for production deployments.

## 2. What is the architecture?

The application is implemented in **C# / .NET** using clear Domain, Application, Infrastructure and API boundaries.

### Core architecture

```text
Client
  |
  v
ASP.NET Core API
  |
  v
Order Service
  |
  +--> PostgreSQL
  |
  +--> Transactional Outbox
          |
          v
   Azure Service Bus
          |
          +--> Order processing / consumers
          |
          +--> Inventory
          |
          +--> Billing simulation
          |
          +--> Fulfillment
          |
          +--> Notifications / Audit
```

### Azure architecture

* **Compute:** AKS
* **Messaging:** Azure Service Bus
* **Database:** Azure Database for PostgreSQL
* **Secrets:** Azure Key Vault
* **Identity:** Microsoft Entra ID + Workload Identity
* **Networking:** Azure VNet, Private Endpoints and Private DNS
* **Container registry:** Azure Container Registry
* **Observability:** OpenTelemetry → Application Insights / Azure Monitor
* **Infrastructure:** Terraform
* **Deployment:** Helm / GitHub Actions

The architecture follows an **event-driven design** where business state and event publication are coordinated through the transactional Outbox pattern. Consumers use durable idempotency state so duplicate delivery does not cause duplicate business actions.

## 3. What senior engineering problems were solved?

The project focuses on engineering problems that go beyond basic CRUD development:

* **Multi-tenancy:** tenant context and authorization are enforced at the application boundary.
* **Reliable messaging:** transactional Outbox ensures that committed business changes have a corresponding publish intent.
* **Idempotent consumers:** Inbox state prevents duplicate message processing.
* **Distributed workflows:** order processing is designed around asynchronous events and Saga-style compensation.
* **Failure handling:** retries, timeouts, dead-letter handling and compensation are part of the architecture.
* **Cloud security:** Azure RBAC, managed identity, Workload Identity and Key Vault eliminate application-managed cloud credentials.
* **Private networking:** PostgreSQL and Key Vault are accessed through private connectivity in the Azure reference architecture.
* **Distributed tracing:** OpenTelemetry propagates correlation/trace context through application and messaging boundaries.
* **Testability:** domain, application, architecture, integration and end-to-end tests validate the system at multiple levels.
* **Operational discipline:** Azure resources are provisioned as infrastructure and can be destroyed after demonstrations to avoid unnecessary cloud costs.

The project demonstrates the principle of **avoiding microservice theater**: service boundaries are introduced where autonomy, scaling or failure isolation provides a real engineering benefit.

## 4. How can I run or inspect the demo?

### Run locally

Prerequisites:

* .NET 10 SDK
* Git
* Docker Desktop, if using containerized dependencies
* Azure CLI only when testing Azure integrations

Clone the repository:

```bash
git clone https://github.com/armsah/EuroTrade-Cloud.git
cd EuroTrade-Cloud
```

Build the solution:

```bash
dotnet build
```

Run the complete automated test suite:

```bash
dotnet test
```

The current test suite validates domain behavior, application behavior, architecture rules, integration scenarios and end-to-end order processing.

### Inspect the Azure implementation

The Azure environment is defined through the project's infrastructure and deployment configuration. When the demo environment is provisioned, the main components include:

* AKS
* Azure Service Bus
* PostgreSQL
* Azure Key Vault
* Azure Container Registry
* VNet and Private Endpoints
* Application Insights / Log Analytics

Azure resources are **not intended to remain running continuously for the portfolio demo**. The environment can be provisioned for demonstration, inspected, and then destroyed to minimize costs.

### Availability and production topology

The provisioned Azure environment is intentionally a **cost-optimized development/demo configuration**, not a claim of full infrastructure-level high availability.

At the application and Kubernetes level, the project demonstrates several resilience practices:

* Multiple API replicas
* Kubernetes liveness and readiness probes
* CPU and memory requests and limits
* Rolling deployment configuration
* PodDisruptionBudget
* Stateless API containers
* Transactional Outbox and durable Inbox/idempotency patterns for messaging reliability

These controls improve workload resilience, but multiple application replicas alone do not guarantee node-level high availability. In a single-node AKS configuration, multiple pods may still run on the same underlying node, so loss of that node can make all replicas unavailable.

A production deployment would extend the current topology according to its availability requirements, typically including:

* Multiple AKS worker nodes
* Multiple availability zones where supported and required
* AKS Cluster Autoscaler
* Kubernetes Horizontal Pod Autoscaler (HPA)
* Topology spread constraints or pod anti-affinity to distribute replicas across nodes or zones
* Production PostgreSQL high availability, backups and tested recovery procedures
* Capacity, disruption and failover testing against defined availability objectives

These additional resources are intentionally not provisioned solely for the portfolio demonstration because doing so would increase recurring Azure costs without materially improving the architectural demonstration.

The software and infrastructure-as-code demonstrate **production-oriented patterns**; the actual demo environment is deliberately **cost-optimized** and is not represented as highly available.

### Inspect the P9 observability implementation

P9 adds OpenTelemetry instrumentation and Azure Application Insights integration.

The intended evidence is a distributed order trace showing a single order operation crossing application and messaging boundaries, allowing the reviewer to inspect:

* Trace ID / correlation
* API operation
* Order processing
* Outbox publication
* Message consumption
* Downstream processing
* Timing and dependencies

See the repository's architecture, observability and evidence documentation for the corresponding diagrams and trace screenshots.

The observability model combines **distributed traces, structured logs, application metrics and Azure platform metrics**.

#### Distributed tracing

Trace context is preserved across the transactional Outbox boundary. The trace context captured when the Outbox record is created is restored when the message is later published, allowing asynchronous operations to remain correlated with the originating request.

The intended distributed order trace allows a reviewer to inspect:

* Trace ID / correlation
* API operation
* Order creation
* Outbox persistence
* Outbox publication
* Azure Service Bus message consumption
* Downstream processing
* Timing and dependencies

#### Application metrics

The application exposes business and reliability metrics through OpenTelemetry:

| Metric | Purpose |
| ------ | ------- |
| `orders_created_total` | Number of successfully created orders |
| `outbox_pending_messages` | Current number of unpublished, non-poison Outbox messages |
| `outbox_publish_failures_total` | Number of failed Outbox publishing attempts |
| `message_processing_duration` | Duration of asynchronous message processing |
| `inbox_duplicate_messages_total` | Number of duplicate messages detected by the Inbox/idempotency mechanism |
| `dead_lettered_messages_total` | Number of messages explicitly dead-lettered by the application |

The Outbox backlog gauge is derived from durable database state. Messages waiting for a retry remain part of the backlog, while successfully published and permanently failed/poison messages are excluded.

#### Azure platform metrics

Infrastructure-owned metrics remain sourced from the Azure services that own that state rather than requiring additional management privileges in the application.

In particular, **dead-letter queue depth is monitored through Azure Service Bus/Azure Monitor**, rather than queried by the application.

This keeps Azure Service Bus management-plane concerns outside the application and preserves the project's least-privilege RBAC model.

Relevant Azure platform telemetry includes:

* Dead-letter queue depth
* Active message count / queue depth
* Incoming and outgoing message activity
* Service Bus server errors
* Service Bus throttling

Application-level `dead_lettered_messages_total` and Azure Service Bus dead-letter queue depth serve different purposes: the former records application dead-letter actions, while the latter represents the current broker-side DLQ backlog.

#### Common telemetry resource attributes

OpenTelemetry resources include common attributes that allow telemetry to be grouped by service, version and deployment environment:

* `service.name`
* `service.version`
* `deployment.environment.name`

Together these attributes make it possible to distinguish telemetry between application versions and environments without introducing high-cardinality dimensions.

The resulting observability model is:

```text
Application
    |
    +--> Distributed traces
    |
    +--> Structured logs
    |
    +--> Business metrics
    |      orders_created_total
    |
    +--> Reliability metrics
           outbox_pending_messages
           outbox_publish_failures_total
           message_processing_duration
           inbox_duplicate_messages_total
           dead_lettered_messages_total

Azure Service Bus / Azure Monitor
    |
    +--> Dead-letter queue depth
    +--> Queue depth
    +--> Broker errors
    +--> Throttling

Common OpenTelemetry resource attributes
    |
    +--> service.name
    +--> service.version
    +--> deployment.environment.name

### Project status

| Phase  | Focus                                                              |
| ------ | ------------------------------------------------------------------ |
| P1–P7  | Application, messaging, resilience, identity and cloud foundation  |
| **P8** | **Private connectivity architecture**                              |
| **P9** | **OpenTelemetry + Application Insights distributed observability** |
| P10+   | Load/failure testing, SLOs and CI/CD hardening                     |

**Previous milestone:** `Complete P8 private connectivity architecture`

**Current milestone:** `Complete P9 event-driven order processing and observability`
