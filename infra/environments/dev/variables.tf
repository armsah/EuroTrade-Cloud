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
