variable "name" {
  type        = string
  description = "Virtual network name."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group containing the VNet."
}

variable "location" {
  type        = string
  description = "Azure region."
}

variable "address_space" {
  type        = list(string)
  description = "VNet address space."
  default     = ["10.20.0.0/16"]
}

variable "aks_subnet_prefixes" {
  type        = list(string)
  description = "AKS subnet address prefixes."
  default     = ["10.20.1.0/24"]
}

variable "private_endpoint_subnet_prefixes" {
  type        = list(string)
  description = "Private endpoint subnet address prefixes."
  default     = ["10.20.2.0/24"]
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to network resources."
  default     = {}
}