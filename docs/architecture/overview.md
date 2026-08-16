# EuroTrade Cloud — Architecture Overview

## 1. Purpose

EuroTrade Cloud is a production-grade, multi-tenant B2B order and fulfillment platform designed for European business customers.

The platform demonstrates senior-level application engineering and cloud engineering practices using C#, ASP.NET Core, PostgreSQL, Azure, AKS, Azure Service Bus, infrastructure as code, secure identity, observability, and automated delivery.

The architecture is designed around the following principles:

- Strong tenant isolation
- Secure API access
- Reliable asynchronous workflows
- Transactional consistency
- Idempotent message processing
- Observable operations
- Controlled and repeatable deployments
- Infrastructure reproducibility
- Explicit architectural decisions
- Measurable reliability and performance

---

## 2. Business Capabilities

The platform supports the following business capabilities:

- Tenant onboarding and configuration
- Customer management
- Product catalog management
- Order creation and lifecycle management
- Inventory reservation and release
- Payment simulation
- Shipment orchestration
- Notifications
- Document generation and storage
- Audit history
- Administrative operations

The initial implementation will focus on the core order workflow before progressively introducing distributed messaging, cloud infrastructure, security hardening, and operational capabilities.

---

## 3. Architecture Strategy

The project will be implemented incrementally.

The initial application will use a modular-monolith architecture with explicit domain and service boundaries. This allows the business capabilities and domain rules to be developed and tested locally without introducing unnecessary distributed-system complexity.

The target architecture will progressively evolve toward independently deployable services where there is a demonstrated operational, scaling, ownership, or failure-isolation reason for the separation.

The initial independently deployable service boundaries are:

1. Tenant Service
2. Catalog Service
3. Order Service
4. Fulfillment Service

Billing, Notification, and Audit capabilities will initially remain simpler components and will be separated only when their operational or scaling requirements justify independent deployment.

This approach intentionally avoids microservice theater.

---

## 4. Target Architecture

The production-reference architecture consists of the following major layers:

```text
                         Internet / Business Clients
                                  |
                                  v
                    +-----------------------------+
                    | Azure Front Door /          |
                    | Application Gateway + WAF   |
                    +-------------+---------------+
                                  |
                                  v
                    +-----------------------------+
                    | API Management              |
                    | Optional API governance     |
                    +-------------+---------------+
                                  |
                                  v
                    +-----------------------------+
                    | AKS                         |
                    | Network-restricted cluster  |
                    +-------------+---------------+
                                  |
              +-----------------+-----------------+
              |                 |                 |
              v                 v                 v
        Tenant Service    Catalog Service    Order Service
                                                  |
                                                  v
                                         Fulfillment Service
                                                  |
                                  +---------------+---------------+
                                  |                               |
                                  v                               v
                         Azure Service Bus              PostgreSQL
                         Topics / Queues                Transactional data
                                  |
                                  +-----------------------+
                                  |
                                  v
                         Async business workflows

                    Supporting Azure Services
                    -------------------------
                    Entra ID
                    Key Vault
                    Blob Storage
                    Application Insights
                    Azure Monitor
                    Managed Prometheus / Grafana
                    Azure Container Registry
```
