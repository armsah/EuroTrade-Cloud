variable "name" {
  type        = string
  description = "Globally unique Key Vault name."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group containing the Key Vault."
}

variable "location" {
  type        = string
  description = "Azure region."
}

variable "tenant_id" {
  type        = string
  description = "Microsoft Entra tenant ID."
}

variable "tags" {
  type        = map(string)
  description = "Resource tags."
  default     = {}
}
