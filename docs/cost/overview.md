# EuroTrade Cloud — Cost Overview

## 1. Purpose

This document defines the initial cost model for the EuroTrade Cloud
multi-tenant B2B order and fulfillment platform.

The objective is to identify the primary Azure cost drivers, establish
cost-management principles, and provide a baseline for estimating and
controlling infrastructure costs.

The cost model is intentionally based on architectural assumptions rather
than production usage. Actual costs will depend on workload volume,
deployment configuration, Azure region, retention requirements, and
service consumption.

This document will evolve as the platform implementation and workload
characteristics become more detailed.

---

## 2. Scope

The cost model covers the primary infrastructure and operational
components of the platform:

- Azure Kubernetes Service (AKS)
- Azure Container Registry
- PostgreSQL
- Azure Service Bus
- Azure Blob Storage
- Azure Key Vault
- Azure Monitor
- Application Insights
- Log Analytics
- Azure Front Door / WAF
- API Management or API Gateway
- Networking
- CI/CD infrastructure
- Backup and recovery
- Development and test environments

The model focuses on:

- Infrastructure cost
- Data storage cost
- Data transfer cost
- Observability cost
- Security-related cost
- Environment cost
- Scaling cost
- Backup and recovery cost

---

## 3. Cost Objectives

The platform should:

1. Maintain predictable infrastructure costs.
2. Avoid unnecessary always-on resources.
3. Use managed Azure services where operational benefits justify the cost.
4. Apply resource sizing based on actual workload requirements.
5. Separate development, test, and production environments.
6. Monitor cost continuously.
7. Prevent uncontrolled resource consumption.
8. Use autoscaling where appropriate.
9. Apply retention policies to logs and telemetry.
10. Identify major cost drivers before production deployment.
11. Review infrastructure costs as workload volume changes.
12. Maintain sufficient capacity for reliability and security requirements.

---

## 4. Cost Model

The platform cost is divided into the following categories:

| Category           | Primary Cost Drivers                               |
| ------------------ | -------------------------------------------------- |
| Compute            | AKS nodes, workloads, autoscaling                  |
| Database           | PostgreSQL compute, storage, backups               |
| Messaging          | Service Bus operations and capacity                |
| Storage            | Blob storage capacity, transactions, redundancy    |
| Networking         | Data transfer, ingress/egress, networking services |
| API Edge           | Front Door, WAF, API Management                    |
| Observability      | Azure Monitor, Application Insights, Log Analytics |
| Security           | Key Vault operations and related security services |
| Container Registry | Image storage and registry operations              |
| CI/CD              | GitHub Actions usage and build resources           |
| Backup             | Database and storage backup retention              |
| Environments       | Development, test, staging, production             |

---

## 5. Major Cost Drivers

### 5.1 AKS

AKS is expected to be one of the primary infrastructure cost drivers.

Potential cost components include:

- Worker node compute
- Node count
- Node VM size
- Autoscaling
- System node requirements
- Workload resource requests
- Persistent storage
- Networking

Cost controls:

- Define CPU and memory requests/limits.
- Use horizontal and cluster autoscaling where appropriate.
- Avoid oversized nodes.
- Separate system and workload requirements where justified.
- Scale non-production environments down when not required.
- Monitor node utilization.

---

### 5.2 PostgreSQL

PostgreSQL cost depends primarily on:

- Compute tier
- vCPU and memory
- Storage capacity
- Backup retention
- High-availability configuration
- I/O requirements
- Database workload

Cost controls:

- Start with an appropriately sized service tier.
- Monitor CPU, memory, storage, and I/O utilization.
- Avoid premature overprovisioning.
- Define backup retention according to business requirements.
- Review database growth periodically.

---

### 5.3 Azure Service Bus

Service Bus costs are influenced by:

- Number of operations
- Messaging volume
- Message size
- Messaging tier
- Number of namespaces
- Retention and dead-letter behavior

Cost controls:

- Keep message payloads appropriately sized.
- Avoid unnecessary message duplication.
- Use efficient event and command contracts.
- Monitor dead-letter queues.
- Review messaging volume as workload increases.

---

### 5.4 Azure Blob Storage

Storage costs depend on:

- Stored data volume
- Access tier
- Transaction volume
- Replication configuration
- Data retrieval
- Data transfer
- Retention period

Cost controls:

- Use appropriate storage tiers.
- Define document retention policies.
- Avoid storing unnecessary generated artifacts.
- Monitor storage growth.
- Apply lifecycle management where appropriate.

---

### 5.5 Observability

Observability can become a significant variable cost.

Relevant services include:

- Azure Monitor
- Application Insights
- Log Analytics

Cost drivers include:

- Log ingestion
- Metric volume
- Trace volume
- Retention
- Query volume
- Diagnostic settings

Cost controls:

- Avoid excessive debug logging in production.
- Do not log sensitive data.
- Define log retention policies.
- Use structured logging.
- Monitor telemetry ingestion volume.
- Keep high-cardinality telemetry under control.

---

## 6. Environment Strategy

The platform should use separate environments for different lifecycle stages.

| Environment | Purpose                       | Cost Strategy                           |
| ----------- | ----------------------------- | --------------------------------------- |
| Development | Local/application development | Minimize always-on resources            |
| Test        | Automated integration testing | Use temporary or right-sized resources  |
| Staging     | Production-like validation    | Smaller production-equivalent footprint |
| Production  | Customer workloads            | Reliability and scalability prioritized |

Development and test environments should not automatically receive the
same capacity as production.

Production capacity should be determined from workload requirements,
availability targets, and performance testing.

---

## 7. Cost Allocation

Azure resources should use consistent tags to support cost analysis.

Recommended tags:

| Tag         | Example        |
| ----------- | -------------- |
| Project     | EuroTradeCloud |
| Environment | Production     |
| Owner       | Platform       |
| Service     | Orders         |
| CostCenter  | Engineering    |
| ManagedBy   | Terraform      |
| Criticality | High           |

Tagging strategy should be applied consistently across infrastructure.

---

## 8. Cost Monitoring

Cost should be monitored through Azure cost-management capabilities and
operational dashboards.

The following should be reviewed:

- Current monthly spend
- Cost by environment
- Cost by Azure service
- Cost trend
- Unexpected spend increases
- Resource utilization
- Storage growth
- Log ingestion
- Network egress

Cost alerts should be configured for important environments.

Production should have stricter cost monitoring than development.

---

## 9. Budget Strategy

Budgets should be defined independently for each environment where
practical.

Example structure:

| Environment | Budget Approach           |
| ----------- | ------------------------- |
| Development | Low fixed budget          |
| Test        | Controlled monthly budget |
| Staging     | Moderate budget           |
| Production  | Workload-based budget     |

Budget thresholds should trigger investigation before costs become
uncontrolled.

Example alert levels:

- 50% — informational
- 75% — review
- 90% — investigation
- 100% — escalation

These thresholds are initial operational guidelines and may be adjusted
after real usage data becomes available.

---

## 10. Cost Optimization Principles

The following principles apply:

### Right-size before scaling

Resources should be sized according to measured workload rather than
assumed maximum capacity.

### Scale automatically where appropriate

Autoscaling should be used where workload variability justifies it.

### Avoid unnecessary always-on infrastructure

Non-production resources should be stopped, scaled down, or provisioned
temporarily when practical.

### Prefer managed services when operational value justifies cost

Managed Azure services reduce operational burden but must still be
evaluated for cost efficiency.

### Monitor before optimizing

Cost optimization should be based on actual usage and telemetry.

### Protect reliability

Cost reduction must not compromise:

- Security
- Tenant isolation
- Availability
- Data durability
- Backup requirements
- Recovery objectives

---

## 11. Cost Risks

The following risks require monitoring:

| Risk                        | Potential Impact           | Mitigation              |
| --------------------------- | -------------------------- | ----------------------- |
| Oversized AKS nodes         | High compute cost          | Monitor utilization     |
| Excessive logging           | High telemetry cost        | Retention and filtering |
| Uncontrolled storage growth | Increasing storage cost    | Lifecycle policies      |
| Excessive network egress    | Unexpected charges         | Monitor traffic         |
| Overprovisioned database    | High database cost         | Right-sizing            |
| Idle development resources  | Waste                      | Scale down or remove    |
| Excessive messaging         | Increased Service Bus cost | Monitor operations      |
| Uncontrolled autoscaling    | Cost spikes                | Define scaling limits   |
| Duplicate environments      | Infrastructure waste       | Environment governance  |

---

## 12. Cost and Architecture Trade-offs

Cost decisions must be evaluated together with architecture decisions.

Examples include:

### AKS vs simpler compute

AKS provides greater orchestration and operational control but introduces
additional infrastructure complexity and baseline compute requirements.

### PostgreSQL sizing

A larger database tier improves capacity but increases fixed cost.

### Observability depth

Detailed telemetry improves diagnostics but increases ingestion and
retention costs.

### High availability

Higher availability configurations improve resilience but generally
increase infrastructure cost.

### Data redundancy

Higher storage redundancy improves durability but increases storage cost.

These trade-offs should be documented in relevant architecture decisions
and ADRs when they materially affect the platform design.

---

## 13. Cost Validation

Cost assumptions should be validated progressively.

Validation activities include:

1. Estimate infrastructure cost before deployment.
2. Deploy representative development infrastructure.
3. Measure actual resource consumption.
4. Compare actual usage against assumptions.
5. Identify major cost deviations.
6. Adjust resource sizing.
7. Repeat during performance testing.
8. Establish production cost baselines.

Cost estimates should not be treated as fixed until representative
workload testing has been completed.

---

## 14. Production Cost Baseline

Before production launch, the project should establish:

- Expected monthly infrastructure cost
- Expected cost per environment
- Expected cost by Azure service
- Expected storage growth
- Expected messaging volume
- Expected telemetry volume
- Expected database growth
- Expected compute utilization
- Expected network traffic

Where practical, unit economics should also be evaluated.

Examples:

- Cost per tenant
- Cost per order
- Cost per 1,000 orders
- Cost per GB stored
- Cost per million messages

These metrics provide a basis for future capacity and pricing analysis.

---

## 15. Review Process

Cost should be reviewed:

- During architecture changes
- Before production deployment
- After major workload changes
- After infrastructure scaling
- During monthly operational reviews
- After significant Azure pricing changes

Unexpected cost increases should be investigated before they become
persistent.

---

## 16. Assumptions

This initial cost model assumes:

- Azure is the primary cloud platform.
- AKS is the selected application runtime.
- PostgreSQL is the primary relational database.
- Azure Service Bus is the messaging platform.
- Azure Blob Storage is used for documents.
- Azure-native monitoring is used for observability.
- Infrastructure is managed through Infrastructure as Code.
- Production workloads will scale according to demand.

Actual costs will depend on Azure region, service tier, workload volume,
availability configuration, retention policies, and network usage.

---

## 17. Related Documents

- `docs/architecture/overview.md`
- `docs/architecture/nfr.md`
- `docs/architecture/tenant-isolation.md`
- `docs/adr/0001-aks-vs-container-apps.md`
- `docs/adr/0002-service-bus-vs-event-hubs.md`
- `docs/adr/0003-postgresql-database.md`
- `docs/adr/0004-tenant-isolation.md`
- `docs/threat-model/overview.md`
- `docs/runbooks/overview.md`
- `docs/testing/overview.md`
