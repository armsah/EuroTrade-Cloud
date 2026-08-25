variable "name" {
  type        = string
  description = "User-assigned managed identity name."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group containing the managed identity."
}

variable "location" {
  type        = string
  description = "Azure region."
}

variable "oidc_issuer_url" {
  type        = string
  description = "AKS OIDC issuer URL."
}

variable "kubernetes_namespace" {
  type        = string
  description = "Kubernetes namespace containing the workload."
}

variable "service_account_name" {
  type        = string
  description = "Kubernetes service account federated with the managed identity."
}

variable "federated_credential_name" {
  type        = string
  description = "Federated identity credential name."
  default     = "aks-workload-identity"
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to the managed identity."
  default     = {}
}