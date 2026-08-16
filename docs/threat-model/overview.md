# EuroTrade Cloud — Threat Model

## 1. Purpose

This document defines the initial threat model for the EuroTrade Cloud
multi-tenant B2B order and fulfillment platform.

The objective is to identify important security threats, define security
boundaries, and establish mitigations that will be validated during
implementation and testing.

The threat model will evolve as the platform architecture becomes more
detailed.

---

## 2. Scope

The threat model covers:

- External clients
- API edge
- ASP.NET Core services
- Microsoft Entra ID authentication
- Tenant authorization
- PostgreSQL
- Azure Service Bus
- Azure Blob Storage
- Azure Key Vault
- AKS
- Workload Identity
- GitHub Actions
- Azure Container Registry
- Application telemetry
- Administrative access

The primary security concerns are:

- Authentication
- Authorization
- Tenant isolation
- Data confidentiality
- Data integrity
- Secret protection
- Message security
- Supply-chain security
- Infrastructure security
- Auditability
- Availability

---

## 3. Security Objectives

The platform must:

1. Authenticate users and application identities securely.
2. Prevent unauthorized access to tenant data.
3. Prevent cross-tenant data access.
4. Protect business data in transit and at rest.
5. Avoid long-lived application secrets.
6. Protect Azure resources through least-privilege identities.
7. Prevent unauthorized message processing.
8. Maintain auditability of security-sensitive operations.
9. Detect important security events.
10. Reduce the impact of compromised application components.
11. Protect the software supply chain.
12. Support secure and controlled deployments.

---

## 4. High-Level Architecture

The security-relevant architecture is:

```text
                         Internet
                            |
                            v
                 Azure Front Door / WAF
                            |
                            v
                    API Management
                    or API Gateway
                            |
                            v
                 ASP.NET Core Services
                    /      |      \
                   /       |       \
                  v        v        v
             PostgreSQL  Service   Blob Storage
                         Bus
                           |
                           v
                    Background Workers


       Microsoft Entra ID
              |
              v
       Authentication
              |
              v
       Tenant Authorization


       GitHub Actions
              |
             OIDC
              |
              v
        Azure Identity
              |
              v
       Azure Resources


       AKS Workload
              |
              v
       Workload Identity
              |
              v
       Azure Resources
```

## 5. Trust Boundaries

The following trust boundaries are identified.

Boundary 1 — External Client to Application

External clients are untrusted.

Requests must be authenticated and authorized before accessing
application resources.

Boundary 2 — Identity to Application

The application trusts validated Microsoft Entra ID tokens and claims
only after authentication and token validation.

Authorization must still be performed by the application.

Boundary 3 — Service to Database

Services access PostgreSQL through controlled application data-access
components.

Tenant ownership must be enforced at the data-access boundary.

Boundary 4 — Service to Service Bus

Messages cross an asynchronous trust boundary.

Consumers must validate message structure, tenant context, authorization
requirements, and resource ownership.

Boundary 5 — Application to Blob Storage

Applications must control access to tenant-owned documents.

Document identifiers must not themselves provide authorization.

Boundary 6 — CI/CD to Azure

GitHub Actions must authenticate to Azure using short-lived federated
identity rather than long-lived credentials.

Boundary 7 — Workload to Azure Resources

AKS workloads must use Workload Identity or another managed identity
mechanism rather than embedded cloud credentials.

## 6. Assets

The following assets require protection:

Asset Security Concern
Tenant data Confidentiality and isolation
Customer data Confidentiality
Product catalog Confidentiality and integrity
Orders Integrity and confidentiality
Inventory information Integrity and availability
Payment simulation data Integrity and confidentiality
Shipment information Integrity
Documents Confidentiality
Audit records Integrity and auditability
Authentication tokens Confidentiality
Azure credentials Confidentiality
Service Bus messages Integrity and confidentiality
Database credentials Confidentiality
Application configuration Confidentiality and integrity
Container images Supply-chain integrity
Infrastructure configuration Integrity
CI/CD workflows Integrity
Logs and telemetry Confidentiality and integrity

## 7. Threat Actors

External Attacker

An unauthenticated or malicious external user attempting to access
application resources.

Compromised Tenant User

A legitimate tenant user whose account has been compromised.

Malicious Tenant User

A tenant user intentionally attempting to access another tenant's data.

Compromised Application Component

A service or workload that has been compromised through a vulnerability.

Supply-Chain Attacker

An attacker attempting to compromise source code, dependencies,
container images, CI/CD workflows, or build infrastructure.

Privileged Administrator

A highly privileged identity that could potentially access or modify
multiple tenants or infrastructure resources.

## 8. Threat Categories

The threat model considers the following categories:

Spoofing
Tampering
Repudiation
Information disclosure
Denial of service
Elevation of privilege

These categories are used to structure the security analysis.

## 9. Threats and Mitigations

T01 — Cross-Tenant Data Access

Threat

A user authorized for Tenant A attempts to access data belonging to
Tenant B.

Potential impact

Confidentiality breach
Regulatory exposure
Loss of customer trust

Mitigations

Server-side tenant authorization
Authorized tenant context
Tenant-scoped repository queries
Tenant ownership validation
Automated cross-tenant authorization tests
Optional PostgreSQL Row-Level Security as defense in depth
T02 — Tenant ID Manipulation

Threat

A client modifies a TenantId supplied in an HTTP request.

Potential impact

Unauthorized access to another tenant.

Mitigations

Do not trust client-supplied tenant identifiers
Validate tenant membership server-side
Derive tenant context from trusted identity and authorization
Fail closed when tenant authorization fails
T03 — Broken Object-Level Authorization

Threat

A user obtains a valid order, document, or customer identifier belonging
to another tenant and attempts to access it.

Mitigations

Authorize every tenant-scoped resource
Apply tenant constraints to data queries
Validate resource ownership
Add integration and end-to-end authorization tests
T04 — Stolen Authentication Token

Threat

An attacker obtains a valid authentication token.

Potential impact

Unauthorized access using the victim's identity.

Mitigations

Microsoft Entra ID
Short-lived access tokens
Least-privilege authorization
Strong authentication policies
Server-side authorization
Monitoring of suspicious activity
T05 — Service Bus Message Tampering or Abuse

Threat

A malicious or compromised component attempts to publish or process
unauthorized messages.

Potential impact

Incorrect business operations or cross-tenant actions.

Mitigations

Azure identity-based access
Least-privilege Service Bus permissions
Message validation
Tenant context validation
Idempotent consumers
Dead-letter queues
Correlation and tracing
T06 — Duplicate Message Processing

Threat

A business message is delivered or processed more than once.

Potential impact

Duplicate inventory reservations, payments, or shipments.

Mitigations

Durable inbox/idempotency store
Business operation identifiers
Idempotent message handlers
Service Bus duplicate detection where appropriate

Broker-level duplicate detection is not treated as a replacement for
receive-side idempotency.

T07 — Database Credential Exposure

Threat

Database credentials are committed to source control or exposed through
application configuration.

Potential impact

Unauthorized database access.

Mitigations

Azure Key Vault where secrets are required
Managed identity / Workload Identity
DefaultAzureCredential
No secrets committed to Git
Secret scanning
Least-privilege database access
T08 — Compromised CI/CD Credentials

Threat

An attacker obtains long-lived deployment credentials.

Potential impact

Unauthorized modification of Azure infrastructure or application
deployments.

Mitigations

GitHub Actions OIDC
Federated Azure identity
Short-lived credentials
Least-privilege permissions
Protected branches
Required reviews for sensitive changes
T09 — Vulnerable Dependency

Threat

A vulnerable third-party dependency is introduced into the application.

Potential impact

Remote code execution, information disclosure, or privilege escalation.

Mitigations

Dependency scanning
Automated security checks
Dependabot or equivalent tooling
Regular dependency updates
CI security gates
T10 — Compromised Container Image

Threat

A malicious or vulnerable container image is deployed to AKS.

Potential impact

Compromise of application workloads or cluster resources.

Mitigations

Build images through controlled CI/CD
Container vulnerability scanning
Azure Container Registry
Image provenance/SBOM
Minimal base images
Non-root containers where practical
Controlled deployment process
T11 — Secret Exposure in Logs

Threat

Tokens, credentials, personal data, or sensitive configuration are
written to application logs.

Potential impact

Information disclosure.

Mitigations

Structured logging
Sensitive-data filtering
No credential logging
Secure telemetry configuration
Log access controls
T12 — Unauthorized Administrative Access

Threat

A privileged administrator accesses tenant data without appropriate
authorization or operational justification.

Potential impact

Large-scale confidentiality breach.

Mitigations

Dedicated administrative roles
Least privilege
Explicit authorization policies
Auditable administrative operations
Monitoring
Separation of administrative and tenant-user privileges
T13 — Denial of Service

Threat

An attacker generates excessive requests or causes resource exhaustion.

Potential impact

Reduced availability.

Mitigations

WAF
Rate limiting
API protections
Kubernetes resource limits
Health probes
Autoscaling where appropriate
Monitoring and alerting
T14 — Insecure Document Access

Threat

A user obtains another tenant's document identifier and attempts to
retrieve the document.

Potential impact

Confidentiality breach.

Mitigations

Tenant-aware authorization
Document ownership validation
Controlled Blob Storage access
No direct authorization based solely on object identifiers
T15 — Supply-Chain Compromise

Threat

Source code, dependencies, build actions, or container artifacts are
compromised.

Potential impact

Malicious code reaching production.

Mitigations

Protected GitHub branches
Dependency scanning
Secret scanning
Code scanning
SBOM generation
Container scanning
Controlled release process
Minimal CI/CD permissions

## 10. Security Controls

The following controls are planned:

Control Implementation
Authentication Microsoft Entra ID
Authorization ASP.NET Core policies/RBAC
Tenant isolation Server-side tenant context
Secret management Azure Key Vault
Azure identity Workload Identity
CI/CD authentication GitHub Actions OIDC
Transport security TLS
Database security PostgreSQL access controls
Messaging security Azure Service Bus authorization
Container security Image scanning
Dependency security Dependency scanning
Supply-chain security SBOM and controlled builds
Auditability Audit service/events
Monitoring OpenTelemetry + Azure Monitor
Kubernetes security RBAC, network controls, resource limits

## 11. Security Testing

Security controls will be validated through automated and manual testing.

Required tests include:

Cross-tenant access rejection
Invalid token rejection
Unauthorized role rejection
Modified TenantId rejection
Cross-tenant document access rejection
Cross-tenant message rejection
Duplicate message handling
Missing tenant context rejection
Secret scanning
Dependency vulnerability scanning
Container image scanning
CI/CD permission validation

Security tests will be implemented progressively under:

tests/
├── unit/
├── integration/
├── architecture/
└── e2e/

## 12. Residual Risk

The initial implementation uses logical tenant isolation within shared
application and database infrastructure.

Residual risks include:

Application authorization defects
Database query mistakes
Compromised application workloads
Privileged administrator misuse
Dependency vulnerabilities
Cloud service misconfiguration

Defense-in-depth controls will reduce, but cannot completely eliminate,
these risks.

Higher-isolation deployment models may be evaluated if regulatory,
contractual, or business requirements change.

## 13. Security Assumptions

The threat model assumes:

Azure platform services are trusted infrastructure dependencies.
Microsoft Entra ID authentication is correctly configured.
TLS is used for external communication.
Production identities follow least-privilege principles.
Source repositories and CI/CD workflows are protected.
Operators follow documented security procedures.
Security-sensitive configuration is not stored in source control.

These assumptions must be revisited as the architecture evolves.

## 14. Implementation Priorities

Security implementation will be progressive.

P1 — Application Foundation
Tenant context
Authorization policies
Tenant-scoped data access
Cross-tenant tests
P2 — Azure Integration
Managed identity
Workload Identity
Key Vault
Service Bus authorization
P3 — Kubernetes
AKS RBAC
Network restrictions
Pod security controls
Resource limits
P4 — CI/CD
GitHub Actions OIDC
Secret scanning
Dependency scanning
Container scanning
SBOM
P5 — Operational Security
Security telemetry
Audit events
Alerts
Incident runbooks
Failure testing

## 15. Related Documents

docs/architecture/overview.md
docs/architecture/context.md
docs/architecture/component.md
docs/architecture/nfr.md
docs/architecture/tenant-isolation.md
docs/adr/0001-aks-vs-container-apps.md
docs/adr/0002-service-bus-vs-event-hubs.md
docs/adr/0003-postgresql-database.md
docs/adr/0004-tenant-isolation.md

```
