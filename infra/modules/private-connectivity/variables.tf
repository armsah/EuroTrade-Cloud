variable "resource_group_name" {
  type        = string
  description = "Resource group containing private networking resources."
}

variable "location" {
  type        = string
  description = "Azure region."
}

variable "vnet_id" {
  type        = string
  description = "Virtual network resource ID."
}

variable "private_endpoint_subnet_id" {
  type        = string
  description = "Subnet used for private endpoints."
}

variable "key_vault_id" {
  type        = string
  description = "Azure Key Vault resource ID."
}

variable "postgresql_id" {
  type        = string
  description = "PostgreSQL Flexible Server resource ID."
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to private networking resources."
  default     = {}
}