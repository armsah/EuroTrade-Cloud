# EuroTrade Cloud — System Context

## 1. Purpose

This document defines the system context for EuroTrade Cloud using the C4
System Context model.

It identifies the people and external systems that interact with the
EuroTrade Cloud platform and establishes the boundary between the platform
and its surrounding environment.

---

## 2. System

### EuroTrade Cloud

EuroTrade Cloud is a multi-tenant B2B order and fulfillment platform for
European business customers.

The platform provides:

- Tenant management
- Customer and product catalog management
- Order creation and lifecycle management
- Inventory reservation
- Payment simulation
- Shipment orchestration
- Notifications
- Document storage
- Audit history
- Administrative operations

The platform is responsible for enforcing tenant isolation, authorization,
business rules, order processing, asynchronous workflows, auditability,
and operational observability.

---

## 3. People

### B2B Customer User

A user belonging to a business customer.

The user can:

- Browse products
- Create orders
- View order status
- View order history
- Access permitted business documents

The user's access is restricted to the tenant to which the user belongs.

### Tenant Administrator

An administrator responsible for configuring and managing a business tenant.

The administrator can:

- Manage tenant configuration
- Manage users and permissions
- Configure tenant settings
- Review orders
- Review operational information
- Access tenant audit information

### Platform Administrator

An internal platform operator responsible for operating the EuroTrade Cloud
platform.

The platform administrator can:

- Monitor platform health
- Investigate failures
- Review operational events
- Manage platform-level configuration
- Execute operational runbooks

---

## 4. External Systems

### Microsoft Entra ID

Provides authentication and identity information for users accessing
protected platform functionality.

EuroTrade Cloud uses identity information from Microsoft Entra ID while
performing tenant-aware authorization inside the application.

### Email / Webhook Delivery Provider

Represents external notification infrastructure used to deliver
notifications to business customers.

The initial project may simulate this integration rather than connecting
to a production email provider.

### External Shipping Provider

Represents a future external shipping integration.

The portfolio implementation will simulate shipment processing rather than
integrating with a real logistics provider.

---

## 5. System Context

```text
                    +----------------------+
                    |   B2B Customer User  |
                    +----------+-----------+
                               |
                               | HTTPS
                               v
                    +----------------------+
                    |                      |
                    |   EuroTrade Cloud    |
                    |                      |
                    | Multi-tenant B2B     |
                    | Order & Fulfillment  |
                    | Platform             |
                    |                      |
                    +----+-----------+-----+
                         |           |
                         |           |
                         v           v
              +----------------+  +----------------------+
              | Microsoft      |  | Email / Webhook      |
              | Entra ID       |  | Delivery Provider    |
              +----------------+  +----------------------+

                    +----------------------+
                    | Tenant Administrator |
                    +----------+-----------+
                               |
                               |
                               v
                    +----------------------+
                    |   EuroTrade Cloud    |
                    +----------------------+

                    +----------------------+
                    | Platform Administrator|
                    +----------+-----------+
                               |
                               v
                    +----------------------+
                    |   EuroTrade Cloud    |
                    +----------------------+
```
