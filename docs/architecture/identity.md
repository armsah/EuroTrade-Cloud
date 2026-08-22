# Identity Architecture

## Overview

EuroTrade Cloud uses Microsoft Entra ID and Azure Workload Identity to provide passwordless authentication from the AKS workload to Azure resources.

The application does not use an Azure client secret. The AKS workload authenticates using a Kubernetes ServiceAccount, an OIDC federated token, and a Microsoft Entra federated identity credential.

## Identity Flow

```text
                         Microsoft Entra ID
                                |
                                | Federated Identity
                                | Credential
                                v
+-------------------------------------------------------+
|                         AKS                           |
|                                                       |
|  +------------------+       +-----------------------+ |
|  | EuroTrade API    |       | Kubernetes            | |
|  | Pod              |------>| ServiceAccount        | |
|  |                  |       | eurotrade             | |
|  +------------------+       +-----------------------+ |
|           |                           |               |
|           | Workload Identity         | OIDC token    |
|           +---------------------------+               |
|                                                       |
+---------------------------+---------------------------+
                            |
                            | Microsoft Entra token
                            v
                   +-------------------+
                   | Azure Key Vault   |
                   | eurotradedevkv    |
                   +-------------------+
                            |
                            | Secrets
                            v
                   EuroTrade application
```

## Components

### Microsoft Entra ID

Microsoft Entra ID provides the identity platform used by the EuroTrade workload to authenticate to Azure resources.

The workload uses Microsoft Entra Workload Identity with OIDC federation rather than storing an Azure client secret in the application.

### AKS Workload Identity

The EuroTrade API runs in Azure Kubernetes Service with Workload Identity enabled.

The Deployment contains:

```yaml
azure.workload.identity/use: "true"
```

The workload uses the Kubernetes ServiceAccount:

```text
eurotrade
```

The AKS workload receives the projected federated identity token through:

```text
/var/run/secrets/azure/tokens/azure-identity-token
```

The relevant environment variables injected into the workload include:

```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_FEDERATED_TOKEN_FILE
AZURE_AUTHORITY_HOST
```

These values allow Azure Identity libraries to authenticate the workload without an application password or client secret.

## Azure Key Vault

The application is configured to use:

```text
KeyVault__Name=eurotradedevkv
```

Azure Key Vault stores application secrets outside the container image and source code.

The workload authenticates to Azure using Workload Identity and Microsoft Entra ID.

## Database Secret

The PostgreSQL connection string is supplied to the application through a Kubernetes Secret:

```text
Secret: eurotrade-ordersdb
Key: connectionString
```

The Deployment maps this value to:

```text
ConnectionStrings__OrdersDb
```

This keeps the database password out of the Helm template and application source code.

## No Application Client Secret

EuroTrade does not store an Azure client secret in:

* application configuration
* Docker images
* Kubernetes Deployment manifests
* Helm values
* source control

Authentication to Azure is based on federated workload identity.

This reduces credential-management overhead and avoids long-lived application secrets.

## Security Model

The identity flow is:

```text
AKS Pod
   |
   | Kubernetes ServiceAccount
   v
AKS Workload Identity
   |
   | OIDC federation
   v
Microsoft Entra ID
   |
   | Access token
   v
Azure resources
   |
   +---- Azure Key Vault
   |
   +---- Other authorized Azure services
```

Access is controlled through Azure RBAC. The workload identity receives only the permissions required by the application.

## Verification

The deployment has been verified in the AKS cluster.

The workload successfully exposes the Azure Workload Identity environment:

```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_FEDERATED_TOKEN_FILE
AZURE_AUTHORITY_HOST
```

The EuroTrade API deployment successfully reaches the Ready state with two replicas.

The health endpoint returns:

```http
HTTP/1.1 200 OK
```

with:

```json
{"status":"healthy"}
```

The Kubernetes Service also successfully routes requests to both EuroTrade API pods.

## P7 Acceptance Criteria

| Requirement                         | Status   |
| ----------------------------------- | -------- |
| Microsoft Entra authentication      | Complete |
| AKS Workload Identity               | Complete |
| Azure Key Vault integration         | Complete |
| Federated identity / OIDC           | Complete |
| No application client secret        | Complete |
| Identity architecture documentation | Complete |
| Identity flow diagram               | Complete |
| Live AKS verification               | Complete |
