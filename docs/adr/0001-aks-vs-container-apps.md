# ADR-0001: Choose AKS over Azure Container Apps

## Status

Accepted

## Date

2026-08-16

## Context

EuroTrade Cloud is a production-reference B2B order and fulfillment
platform designed to demonstrate application engineering, cloud
architecture, Kubernetes operations, security, messaging, observability,
and deployment practices.

The platform requires:

- Multiple independently deployable C# services.
- Controlled rolling deployments.
- Kubernetes health probes.
- Pod disruption budgets.
- Separate system and user node pools.
- Helm-based service deployment.
- Workload Identity integration.
- Horizontal scaling.
- Failure testing.
- Kubernetes-oriented observability.
- A deployment model suitable for a portfolio-scale production reference
  architecture.

Two Azure compute options were considered:

1. Azure Kubernetes Service (AKS)
2. Azure Container Apps

## Decision

EuroTrade Cloud will use **Azure Kubernetes Service (AKS)** as the primary
container orchestration platform.

The initial development environment will use a cost-conscious AKS
configuration. The architecture will distinguish between the demo
environment and the higher-capacity production-reference topology.

Services will be deployed to AKS using Helm.

## Rationale

AKS provides direct Kubernetes capabilities that align with the project's
learning and portfolio objectives.

The project specifically needs to demonstrate:

- Kubernetes deployments
- Services
- Health probes
- Pod disruption budgets
- Node pools
- Horizontal scaling
- Helm
- Workload Identity
- Kubernetes failure scenarios
- Controlled rollout strategies

AKS therefore provides stronger evidence for the intended senior-level
cloud and platform engineering skills.

Azure Container Apps would reduce operational overhead, but it would hide
or abstract several Kubernetes concepts that this project intentionally
needs to demonstrate.

## Alternatives Considered

### Azure Container Apps

Advantages:

- Lower operational overhead.
- Simpler application deployment.
- Managed container platform.
- Suitable for many HTTP and event-driven workloads.

Disadvantages for this project:

- Less direct Kubernetes operational experience.
- Less opportunity to demonstrate node pools and Kubernetes primitives.
- Helm-based Kubernetes deployment would not be the primary deployment
  model.
- Provides less evidence of AKS-specific operational skills.

### Azure Kubernetes Service

Advantages:

- Full Kubernetes orchestration.
- Native support for Helm.
- Node pool control.
- Kubernetes-native health and scaling mechanisms.
- Strong integration with Azure identity and networking.
- Suitable for the production-reference architecture.
- Provides meaningful operational and failure-testing scenarios.

Disadvantages:

- Higher operational complexity.
- Higher cost than simpler managed container options.
- Requires Kubernetes knowledge and operational discipline.

## Consequences

### Positive

- The project demonstrates practical Kubernetes skills.
- Helm becomes the standard service deployment mechanism.
- Kubernetes health and availability mechanisms can be demonstrated.
- Workload Identity can be integrated with Azure resources.
- The project can demonstrate controlled rolling deployments.
- AKS failure and recovery scenarios can be tested.

### Negative

- The project requires additional Kubernetes configuration.
- AKS introduces more infrastructure and operational complexity.
- Development resources must be managed carefully to control cost.

## Implementation Notes

The initial architecture will use:

```text
AKS Cluster
├── System Node Pool
└── User Node Pool
    ├── Tenant Service
    ├── Catalog Service
    ├── Order Service
    └── Fulfillment Service
```

Services will be packaged and deployed using Helm.

Health checks, readiness probes, liveness probes, resource requests,
resource limits, and PodDisruptionBudgets will be introduced during the
AKS implementation phase.

## Related Documentation

- `docs/architecture/overview.md`
- `docs/architecture/context.md`
- `docs/architecture/component.md`
- `docs/architecture/nfrs.md`
