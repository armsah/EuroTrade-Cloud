\# P8 — Private Connectivity Architecture



\## Overview



The EuroTrade Cloud development environment implements a production-reference

Azure network architecture using a dedicated Virtual Network, isolated

subnets, Azure Private Link and Private DNS.



The goal of this layer is to provide private connectivity to sensitive

platform services while keeping the architecture suitable as a foundation

for a production environment.



\## Network Architecture



```mermaid

flowchart TB



&#x20;   subgraph AZURE\["Azure — West Europe"]



&#x20;       subgraph RG\["Resource Group: rg-eurotrade-dev"]



&#x20;           subgraph VNET\["VNet: vnet-eurotrade-dev — 10.20.0.0/16"]



&#x20;               subgraph AKS\_SUBNET\["Subnet: snet-aks — 10.20.1.0/24"]

&#x20;                   AKS\["AKS<br/>aks-eurotrade-dev"]

&#x20;               end



&#x20;               subgraph PE\_SUBNET\["Subnet: snet-private-endpoints — 10.20.2.0/24"]

&#x20;                   PE\_KV\["Private Endpoint<br/>pe-keyvault"]

&#x20;                   PE\_PG\["Private Endpoint<br/>pe-postgresql"]

&#x20;               end



&#x20;           end



&#x20;           KV\["Azure Key Vault<br/>eurotradedevkv"]



&#x20;           PG\["Azure PostgreSQL Flexible Server<br/>eurotrade-dev-postgresql"]



&#x20;           ACR\["Azure Container Registry<br/>eurotradedevacr"]



&#x20;           LAW\["Log Analytics Workspace<br/>law-eurotrade-dev"]



&#x20;           DNS\_KV\["Private DNS Zone<br/>privatelink.vaultcore.azure.net"]



&#x20;           DNS\_PG\["Private DNS Zone<br/>privatelink.postgres.database.azure.com"]



&#x20;       end



&#x20;   end



&#x20;   AKS -->|"AcrPull"| ACR



&#x20;   PE\_KV -->|"Azure Private Link"| KV



&#x20;   PE\_PG -->|"Azure Private Link"| PG



&#x20;   DNS\_KV -.->|"Private DNS resolution"| PE\_KV



&#x20;   DNS\_PG -.->|"Private DNS resolution"| PE\_PG



&#x20;   AKS -.->|"Monitoring"| LAW

```



\## Network Components



\### Virtual Network



\- Name: `vnet-eurotrade-dev`

\- Address space: `10.20.0.0/16`

\- Location: `westeurope`



\### AKS Subnet



\- Name: `snet-aks`

\- Address range: `10.20.1.0/24`

\- Purpose: AKS node workloads



\### Private Endpoint Subnet



\- Name: `snet-private-endpoints`

\- Address range: `10.20.2.0/24`

\- Purpose: Azure Private Endpoints for sensitive services



\## Private Connectivity



The architecture uses Azure Private Link to provide private connectivity

between the VNet and sensitive Azure platform services.



\### Key Vault



\- Private Endpoint: `pe-keyvault`

\- Target: `eurotradedevkv`

\- Private DNS zone: `privatelink.vaultcore.azure.net`

\- RBAC authorization: enabled

\- Public network access: disabled by Terraform configuration



\### PostgreSQL



\- Private Endpoint: `pe-postgresql`

\- Target: `eurotrade-dev-postgresql`

\- Private DNS zone: `privatelink.postgres.database.azure.com`

\- Public network access: disabled



\## Private DNS



Private DNS zones provide name resolution for Azure Private Link endpoints

from resources connected to the Virtual Network.



\### Key Vault DNS



```text

privatelink.vaultcore.azure.net

```



Linked to:



```text

vnet-eurotrade-dev

```



\### PostgreSQL DNS



```text

privatelink.postgres.database.azure.com

```



Linked to:



```text

vnet-eurotrade-dev

```



Both DNS zones are managed by Terraform and linked to the application

Virtual Network.



\## Security Model



Sensitive services are accessed through private connectivity rather than

direct public network access.



The architecture provides:



\- Dedicated VNet

\- Dedicated AKS subnet

\- Dedicated Private Endpoint subnet

\- Azure Private Link

\- Private DNS resolution

\- RBAC-enabled Key Vault

\- PostgreSQL with public network access disabled

\- Terraform-managed infrastructure

\- AKS-to-ACR access through Azure RBAC

\- No ACR administrator credentials required by AKS



\## Sensitive Services



\### Azure Key Vault



Key Vault is protected using Azure RBAC and a Private Endpoint.



The Terraform configuration uses:



```hcl

rbac\_authorization\_enabled = true

public\_network\_access\_enabled = false

```



Private connectivity is provided through:



```text

pe-keyvault

```



and:



```text

privatelink.vaultcore.azure.net

```



\### PostgreSQL Flexible Server



PostgreSQL is configured with public network access disabled.



Private connectivity is provided through:



```text

pe-postgresql

```



and:



```text

privatelink.postgres.database.azure.com

```



This provides the reference architecture for keeping the database off the

public network.



\## Monitoring



Azure Monitor / Log Analytics is deployed through:



```text

law-eurotrade-dev

```



The AKS cluster is connected to the Log Analytics workspace for monitoring

and Container Insights.



\## Container Registry



Azure Container Registry:



```text

eurotradedevacr

```



Login server:



```text

eurotradedevacr.azurecr.io

```



AKS has an `AcrPull` role assignment allowing the cluster to pull container

images from the registry without using registry administrator credentials.



\## Infrastructure Resources



The P8 environment contains the following major components:



| Component | Resource |

|---|---|

| Resource Group | `rg-eurotrade-dev` |

| Virtual Network | `vnet-eurotrade-dev` |

| AKS Subnet | `snet-aks` |

| Private Endpoint Subnet | `snet-private-endpoints` |

| AKS | `aks-eurotrade-dev` |

| Container Registry | `eurotradedevacr` |

| Key Vault | `eurotradedevkv` |

| PostgreSQL | `eurotrade-dev-postgresql` |

| Log Analytics | `law-eurotrade-dev` |

| Key Vault Private Endpoint | `pe-keyvault` |

| PostgreSQL Private Endpoint | `pe-postgresql` |

| Key Vault Private DNS | `privatelink.vaultcore.azure.net` |

| PostgreSQL Private DNS | `privatelink.postgres.database.azure.com` |



\## Validation



The infrastructure was validated using Terraform and Azure CLI.



\### Terraform Validation



```text

terraform validate



Success! The configuration is valid.

```



\### Terraform Plan



The final Terraform plan reports:



```text

No changes. Your infrastructure matches the configuration.



Terraform has compared your real infrastructure against your configuration

and found no differences, so no changes are needed.

```



\### AKS



```text

Name               ProvisioningState    PowerState

\-----------------  -------------------  ------------

aks-eurotrade-dev  Succeeded            Running

```



\### Azure Container Registry



```text

Name             LoginServer                 ProvisioningState

\---------------  --------------------------  -------------------

eurotradedevacr  eurotradedevacr.azurecr.io  Succeeded

```



\### PostgreSQL



```text

Name                      PublicNetworkAccess    State

\------------------------  ---------------------  -------

eurotrade-dev-postgresql  Disabled               Ready

```



\### Private Endpoints



```text

Name           ProvisioningState

\-------------  -------------------

pe-keyvault    Succeeded

pe-postgresql  Succeeded

```



\## P8 Outcome



P8 establishes the private connectivity foundation for the EuroTrade Cloud

platform.



The resulting architecture provides:



1\. A dedicated Azure Virtual Network.

2\. Isolated AKS and Private Endpoint subnets.

3\. Azure Private Link connectivity for sensitive services.

4\. Private DNS resolution for Private Link endpoints.

5\. PostgreSQL with public network access disabled.

6\. RBAC-enabled Azure Key Vault.

7\. Private connectivity to Key Vault.

8\. AKS-to-ACR access through Azure RBAC.

9\. Centralized AKS monitoring through Log Analytics.

10\. Terraform-managed and reproducible infrastructure.



This creates a production-reference networking layer while keeping the

development environment appropriately scoped for the EuroTrade Cloud

portfolio project.



\## P8 Status



\*\*Status: Complete\*\*



The P8 infrastructure has been deployed successfully and verified through

Terraform and Azure CLI.



The architecture documentation and network diagram are maintained in this

repository as part of the project's infrastructure documentation.

