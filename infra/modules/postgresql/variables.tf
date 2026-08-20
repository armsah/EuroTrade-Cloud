variable "name" {
  type        = string
  description = "Globally unique PostgreSQL Flexible Server name."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group containing the PostgreSQL server."
}

variable "location" {
  type        = string
  description = "Azure region."
}

variable "administrator_login" {
  type        = string
  description = "PostgreSQL administrator username."
}

variable "administrator_password" {
  type        = string
  sensitive   = true
  description = "PostgreSQL administrator password."
}

variable "database_name" {
  type        = string
  description = "Application database name."
  default     = "eurotrade"
}

variable "sku_name" {
  type        = string
  description = "PostgreSQL Flexible Server SKU."
  default     = "B_Standard_B1ms"
}

variable "storage_mb" {
  type        = number
  description = "PostgreSQL storage size in MB."
  default     = 32768
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to PostgreSQL resources."
  default     = {}
}