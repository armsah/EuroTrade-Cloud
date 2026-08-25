variable "name" {
  type        = string
  description = "Log Analytics workspace name."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group containing the workspace."
}

variable "location" {
  type        = string
  description = "Azure region."
}

variable "tags" {
  type        = map(string)
  description = "Resource tags."
  default     = {}
}

variable "application_insights_name" {
  type        = string
  description = "Application Insights resource name."
}
