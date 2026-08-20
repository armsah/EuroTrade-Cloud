variable "name" {
  type        = string
  description = "AKS cluster name."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group containing the AKS cluster."
}

variable "location" {
  type        = string
  description = "Azure region for the AKS cluster."
}

variable "dns_prefix" {
  type        = string
  description = "DNS prefix used by the AKS API server."
}

variable "kubernetes_version" {
  type        = string
  description = "Kubernetes version for the AKS cluster."
  default     = null
}

variable "acr_id" {
  type        = string
  description = "Resource ID of the Azure Container Registry."
}

variable "log_analytics_workspace_id" {
  type        = string
  description = "Resource ID of the Log Analytics workspace."
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to Azure resources."
}

variable "tenant_id" {
  type        = string
  description = "Microsoft Entra tenant ID."
}

variable "node_vm_size" {
  description = "VM size for the AKS system node pool"
  type        = string
  default     = "Standard_B2s_v2"
}