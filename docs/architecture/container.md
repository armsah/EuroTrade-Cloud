# EuroTrade Cloud — Container Architecture

## 1. Purpose

This document defines the C4 Container-level architecture for EuroTrade
Cloud.

The container view describes the major applications, services, data stores,
and infrastructure dependencies required to implement the platform.

The architecture is intentionally designed to evolve from a modular
monolith toward independently deployable services where operational,
scaling, ownership, or failure-isolation requirements justify the split.

---

## 2. Initial Service Boundaries

The initial independently deployable service boundaries are:

1. Tenant Service
2. Catalog Service
3. Order Service
4. Fulfillment Service

The following capabilities are initially kept simpler and may be separated
later:

- Billing Simulator
- Notification Service
- Audit Service

This avoids premature microservice decomposition.

---

## 3. Container Overview

```text
                         B2B Customer / Admin
                                  |
                                  | HTTPS
                                  v
                    +-----------------------------+
                    | API Gateway / Edge           |
                    | Routing + Rate Limiting     |
                    +-------------+---------------+
                                  |
                                  v
              +-------------------------------------------+
              |              EuroTrade Cloud               |
              |                                             |
              |  +-------------+    +------------------+   |
              |  | Tenant      |    | Catalog          |   |
              |  | Service     |    | Service          |   |
              |  +------+------+    +--------+---------+   |
              |         |                    |             |
              |         +---------+----------+             |
              |                   |                        |
              |                   v                        |
              |          +------------------+              |
              |          | Order Service    |              |
              |          +--------+---------+              |
              |                   |                        |
              |                   v                        |
              |          +------------------+              |
              |          | Fulfillment      |              |
              |          | Service          |              |
              |          +------------------+              |
              |                                             |
              +-------------------+-------------------------+
                                  |
              +-------------------+-------------------+
              |                   |                   |
              v                   v                   v
       +-------------+     +-------------+     +-------------+
       | PostgreSQL  |     | Service Bus |     | Blob        |
       |             |     |             |     | Storage     |
       +-------------+     +-------------+     +-------------+
```
