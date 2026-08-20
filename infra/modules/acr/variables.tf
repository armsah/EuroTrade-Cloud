variable "name" {
  type        = string
  description = "Globally unique Azure Container Registry name."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group containing the registry."
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
