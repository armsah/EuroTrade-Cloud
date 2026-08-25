variable "subscription_id" {
  type        = string
  description = "Azure subscription ID."
}

variable "tenant_id" {
  type        = string
  description = "Microsoft Entra tenant ID."
}

variable "location" {
  type        = string
  description = "Azure region for the demo environment."
  default     = "westeurope"
}

variable "environment" {
  type        = string
  description = "Environment name."
  default     = "dev"
}

variable "project_name" {
  type        = string
  description = "Project name used in resource naming."
  default     = "eurotrade"
}

variable "postgres_admin_password" {
  type        = string
  sensitive   = true
  description = "PostgreSQL administrator password supplied through TF_VAR_postgres_admin_password."
}

variable "kubernetes_namespace" {
  type        = string
  description = "Kubernetes namespace used by the EuroTrade workload."
  default     = "default"
}

variable "kubernetes_service_account_name" {
  type        = string
  description = "Kubernetes service account used by the EuroTrade workload."
  default     = "eurotrade"
}
