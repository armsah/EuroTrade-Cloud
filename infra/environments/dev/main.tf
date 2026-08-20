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

  name                = "law-${local.name_prefix}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
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