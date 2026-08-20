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
