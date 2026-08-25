locals {
  name_prefix = "${var.project_name}-${var.environment}"

  tags = {
    Project     = "EuroTrade Cloud"
    Environment = var.environment
    ManagedBy   = "Terraform"
    Purpose     = "Portfolio Demo"
  }
}

module "resource_group" {
  source = "../../modules/resource-group"

  name     = "rg-${local.name_prefix}"
  location = var.location
  tags     = local.tags
}

module "acr" {
  source = "../../modules/acr"

  name                = replace("${var.project_name}${var.environment}acr", "-", "")
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
}

module "key_vault" {
  source = "../../modules/key-vault"

  name                = replace("${var.project_name}-${var.environment}-kv", "-", "")
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tenant_id           = var.tenant_id
  tags                = local.tags
}

module "monitoring" {
  source = "../../modules/monitoring"

  name                      = "law-${local.name_prefix}"
  application_insights_name = "appi-${local.name_prefix}"

  resource_group_name = module.resource_group.name
  location            = module.resource_group.location

  tags = local.tags
}

module "service_bus" {
  source = "../../modules/service-bus"

  name = replace(
    "${var.project_name}-${var.environment}-servicebus",
    "-",
    ""
  )

  resource_group_name = module.resource_group.name
  location            = module.resource_group.location

  queue_name = "orders"

  tags = local.tags
}

module "network" {
  source = "../../modules/network"

  name                = "vnet-${local.name_prefix}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location

  address_space                    = ["10.20.0.0/16"]
  aks_subnet_prefixes              = ["10.20.1.0/24"]
  private_endpoint_subnet_prefixes = ["10.20.2.0/24"]

  tags = local.tags
}

module "aks" {
  source = "../../modules/aks"

  name                = "aks-${local.name_prefix}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  dns_prefix          = "aks-${local.name_prefix}"
  tenant_id           = var.tenant_id

  acr_id                     = module.acr.id
  log_analytics_workspace_id = module.monitoring.id

  subnet_id = module.network.aks_subnet_id

  tags = local.tags
}

module "workload_identity" {
  source = "../../modules/workload-identity"

  name                = "id-${local.name_prefix}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location

  oidc_issuer_url = module.aks.oidc_issuer_url

  kubernetes_namespace = var.kubernetes_namespace
  service_account_name = var.kubernetes_service_account_name

  tags = local.tags
}

module "postgresql" {
  source = "../../modules/postgresql"

  name                = "${local.name_prefix}-postgresql"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location

  administrator_login    = "eurotradeadmin"
  administrator_password = var.postgres_admin_password

  database_name = "eurotrade"
  sku_name      = "B_Standard_B1ms"
  storage_mb    = 32768

  tags = local.tags
}

module "private_connectivity" {
  source = "../../modules/private-connectivity"

  resource_group_name        = module.resource_group.name
  location                   = module.resource_group.location
  vnet_id                    = module.network.id
  private_endpoint_subnet_id = module.network.private_endpoint_subnet_id

  key_vault_id   = module.key_vault.id
  postgresql_id  = module.postgresql.id
  service_bus_id = module.service_bus.id

  tags = local.tags
}