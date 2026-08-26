# EuroTrade Cloud

**Production-oriented multi-tenant B2B order-processing platform built
with .NET and Azure.**

EuroTrade Cloud is a cloud-native order-processing platform created to
demonstrate how I would design a **reliable enterprise application**,
rather than simply a CRUD API.

It uses **Clean Architecture, PostgreSQL, Azure Service Bus, AKS,
Terraform and Helm**. A central design decision is the **Transactional
Outbox pattern**, which provides reliable coordination between database
transactions and asynchronous messaging.

The project also incorporates **Microsoft Entra ID, Workload Identity,
tenant-aware authorization and OpenTelemetry** for security and
observability.

The software and infrastructure-as-code demonstrate production-oriented
engineering patterns. The actual Azure demo environment is deliberately
**cost-optimized** and is not represented as a fully highly available
production environment.

------------------------------------------------------------------------

## 1. What problem is solved?

Enterprise order processing involves more than storing an order in a
database.

The system must handle questions such as:

-   What happens if PostgreSQL commits an order but message publication
    fails?
-   What happens if the same message is delivered twice?
-   What happens if the application crashes between messaging
    operations?
-   How is one tenant prevented from accessing another tenant's
    resources?
-   How can an HTTP request be correlated with asynchronous processing
    later?
-   How can the application access Azure services without long-lived
    credentials?

EuroTrade Cloud focuses on these reliability, consistency, security and
operational concerns.

The implemented foundation includes:

-   Order creation and lifecycle management
-   PostgreSQL persistence
-   Transactional Outbox
-   Durable Inbox / idempotency
-   Azure Service Bus messaging
-   Retry, poison-message and dead-letter handling
-   Tenant-aware authorization
-   Entra ID authentication and Azure RBAC
-   Workload Identity
-   OpenTelemetry tracing and application metrics
-   Terraform infrastructure
-   Helm / Kubernetes deployment
-   Container and Kubernetes runtime hardening

Inventory, payment, fulfillment and Saga orchestration are **planned
next milestones**, not completed functionality.

### Capability status

  Capability                                      Status
  ----------------------------------------------- --------------------------
  Order domain and API                            **Implemented**
  PostgreSQL persistence                          **Implemented**
  Transactional Outbox                            **Implemented**
  Durable Inbox / duplicate protection            **Implemented**
  Idempotent order creation                       **Implemented**
  Azure Service Bus                               **Implemented**
  Retry / poison-message / dead-letter handling   **Implemented**
  Entra ID authentication                         **Implemented**
  Tenant-aware authorization                      **Implemented**
  Workload Identity / Azure RBAC                  **Implemented**
  OpenTelemetry tracing                           **Implemented**
  Business / reliability metrics                  **Implemented**
  Terraform infrastructure                        **Implemented**
  Helm / Kubernetes deployment                    **Implemented**
  Container / Kubernetes hardening                **Implemented**
  Inventory                                       **Planned**
  Payment                                         **Planned**
  Fulfillment                                     **Planned**
  Saga orchestration / compensation               **Planned**
  Multi-node / multi-zone AKS                     **Production extension**
  HPA / cluster autoscaling                       **Production extension**
  PostgreSQL HA                                   **Production extension**

------------------------------------------------------------------------

## 2. What is the architecture?

The application uses **Clean Architecture** with explicit boundaries:

**Domain → Application → Infrastructure → API**

-   **Domain:** core business model and rules
-   **Application:** use cases and abstractions
-   **Infrastructure:** PostgreSQL, Azure Service Bus and external
    concerns
-   **API:** HTTP delivery/interface layer
-   **Architecture tests:** automatically verify dependency rules

### Reliable messaging

The central consistency problem is:

> **What happens if PostgreSQL succeeds but Azure Service Bus fails?**

EuroTrade Cloud addresses this using the **Transactional Outbox
pattern**.

The **Order and Outbox event are persisted in the same PostgreSQL
transaction**. Once that transaction commits, the Outbox publisher sends
the event to Azure Service Bus.

If publication fails, the durable Outbox record remains available for
retry. This avoids the classic dual-write inconsistency where an order
is committed but its event is lost.

### At-least-once delivery and idempotency

The system assumes **at-least-once delivery**, not exactly-once
delivery.

For example, a message can be published successfully and the application
can crash before the Outbox row is marked as published. After restart,
the same event may be published again.

Consumers therefore use **durable Inbox/idempotency state** so duplicate
delivery does not cause the business operation to execute twice.

### Azure and deployment

The reference architecture uses:

-   **AKS** --- container orchestration and deployment target
-   **PostgreSQL** --- transactional persistence
-   **Azure Service Bus** --- asynchronous business messaging
-   **Terraform** --- Azure infrastructure provisioning
-   **Helm** --- Kubernetes application packaging and configuration
-   **Microsoft Entra ID** --- authentication
-   **Workload Identity** --- Azure authentication without long-lived
    application secrets
-   **Azure Key Vault / RBAC** --- secret management and least-privilege
    access
-   **OpenTelemetry** --- distributed tracing and application
    observability

The canonical Kubernetes configuration is maintained under
`deploy/helm/eurotrade/`.

------------------------------------------------------------------------

## 3. What senior engineering problems were solved?

### Transactional consistency

Database persistence and broker publication are separate operations. The
Transactional Outbox makes the publication intent part of the same
transaction as the business state.

**Key principle:** the Order and Outbox event are committed together.

### At-least-once messaging

Reliable publication does not imply exactly-once processing.

The implementation therefore combines:

-   Transactional Outbox
-   Durable Inbox
-   Idempotent order creation
-   Duplicate detection
-   PostgreSQL concurrency protection

Critical concurrency scenarios are tested against PostgreSQL using
Testcontainers.

### Failure and concurrency handling

The implementation considers failure paths such as:

-   PostgreSQL failure
-   Azure Service Bus publication failure
-   Application crashes
-   Duplicate message delivery
-   Concurrent Outbox publishers
-   Retry scheduling
-   Poison messages
-   Dead-letter handling
-   Transaction rollback
-   Cancellation propagation

This is deliberately broader than testing only the happy path.

### Security boundaries

The project treats authentication and authorization as separate
concerns:

-   **Authentication:** Who are you?
-   **Authorization:** Can you perform this operation?
-   **Tenant authorization:** Can you access this tenant's resource?

Tenant-aware resource access is enforced using trusted application
context rather than accepting tenant ownership from untrusted request
data.

Azure workloads use **Entra ID, RBAC and Workload Identity** instead of
embedded long-lived cloud credentials.

The application container also runs as a **non-root user**, while
Kubernetes applies restrictions including `runAsNonRoot`,
`allowPrivilegeEscalation: false`, a read-only root filesystem, dropped
Linux capabilities and the default seccomp profile.

### Observability

OpenTelemetry provides distributed tracing across synchronous and
asynchronous boundaries.

Trace context captured during order creation is preserved through the
Outbox and restored during later publication, allowing an HTTP order
request to be correlated with downstream asynchronous processing.

Application metrics include:

  -----------------------------------------------------------------------
  Metric                              Purpose
  ----------------------------------- -----------------------------------
  `orders_created_total`              Successfully created orders

  `outbox_pending_messages`           Current unpublished Outbox backlog

  `outbox_publish_failures_total`     Failed publication attempts

  `message_processing_duration`       Message-processing duration

  `inbox_duplicate_messages_total`    Duplicate messages detected

  `dead_lettered_messages_total`      Messages dead-lettered by
                                      application processing
  -----------------------------------------------------------------------

Azure Monitor / Service Bus provides infrastructure-owned telemetry such
as queue depth, DLQ depth, broker errors and throttling.

Common telemetry resource attributes include `service.name`,
`service.version` and `deployment.environment.name`.

------------------------------------------------------------------------

## 4. How can I run or inspect the demo?

### Prerequisites

-   .NET 10 SDK
-   Git
-   Docker Desktop
-   Helm for Kubernetes chart validation
-   Azure CLI when testing Azure integrations

### Clone and build

``` bash
git clone https://github.com/armsah/EuroTrade-Cloud.git
cd EuroTrade-Cloud
dotnet build
```

### Run the automated tests

``` bash
dotnet test
```

The suite covers domain, application, architecture, PostgreSQL
integration and end-to-end API behavior, including authorization, tenant
isolation, idempotency, concurrency, Outbox retries, rollback and
failure handling.

### Inspect the Kubernetes deployment

``` bash
helm lint ./deploy/helm/eurotrade
helm template eurotrade ./deploy/helm/eurotrade
```

### Inspect the Azure implementation

Terraform and Helm define the Azure reference environment, including
AKS, Azure Service Bus, PostgreSQL, Key Vault, Azure Container Registry,
private networking and Azure Monitor / Application Insights.

The Azure environment is intended to be provisioned for demonstrations
and destroyed afterward to control recurring cloud costs.

------------------------------------------------------------------------

## Current limitations and next milestones

The current project deliberately concentrates on getting the
**order-processing, messaging, security, observability and
infrastructure foundation** correct first.

The next business capabilities are:

1.  Inventory reservation
2.  Payment processing/simulation
3.  Fulfillment workflow
4.  Explicit Saga state
5.  Compensation behavior
6.  Workflow-specific failure testing
7.  Cross-service observability

These capabilities are **planned and are not represented as completed
functionality**.

The demo environment is also not presented as fully highly available. A
continuously operated production deployment would typically add:

-   Multiple AKS worker nodes
-   Availability-zone distribution where required
-   Horizontal Pod Autoscaler
-   Cluster Autoscaler
-   Topology spread constraints or pod anti-affinity
-   Production PostgreSQL HA and tested recovery
-   Operational SLOs, alerts and capacity testing

------------------------------------------------------------------------

## Engineering focus

EuroTrade Cloud is intended to demonstrate engineering judgment rather
than simply the number of technologies used.

The main lessons are:

1.  **Transactional consistency** --- coordinate business state and
    messaging with the Outbox pattern.
2.  **At-least-once messaging** --- expect duplicates and design
    consumers to be idempotent.
3.  **Security boundaries** --- distinguish authentication,
    authorization and tenant ownership.
4.  **Concurrency and failures** --- design and test for crashes,
    retries, duplicates and concurrent processing.
5.  **Infrastructure and operations** --- use cloud identity,
    reproducible infrastructure, hardened containers and end-to-end
    observability.

> **The goal is not to claim that every enterprise workflow is complete.
> The goal is to demonstrate a reliable foundation and make the boundary
> between implemented functionality, planned capabilities and production
> extensions explicit.**
