# EuroTrade Cloud

**Production-grade multi-tenant B2B order platform for European companies.**

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

The project is intentionally designed as a **portfolio-grade production reference**, with infrastructure that can be recreated and destroyed to control cloud costs.

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
* **Private networking:** PostgreSQL and Key Vault are accessed through private connectivity in the production-reference topology.
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

### Project status

| Phase  | Focus                                                              |
| ------ | ------------------------------------------------------------------ |
| P1–P7  | Application, messaging, resilience, identity and cloud foundation  |
| **P8** | **Private connectivity architecture**                              |
| **P9** | **OpenTelemetry + Application Insights distributed observability** |
| P10+   | Load/failure testing, SLOs and CI/CD hardening                     |

**Previous milestone:** `Complete P8 private connectivity architecture`
**Current milestone:** `Complete P9 event-driven order processing and observability`

