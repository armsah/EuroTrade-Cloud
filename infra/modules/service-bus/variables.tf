variable "name" {
  type        = string
  description = "Globally unique Azure Service Bus namespace name."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group containing the Service Bus namespace."
}

variable "location" {
  type        = string
  description = "Azure region."
}

variable "queue_name" {
  type        = string
  description = "Name of the application Service Bus queue."
}

variable "sku" {
  type        = string
  description = "Service Bus namespace SKU."

  # Premium is required for Private Link/private endpoints.
  default = "Premium"

  validation {
    condition     = contains(["Premium"], var.sku)
    error_message = "The EuroTrade environment requires Service Bus Premium because private connectivity is enabled."
  }
}

variable "capacity" {
  type        = number
  description = "Messaging units for a Premium Service Bus namespace."
  default     = 1
}

variable "public_network_access_enabled" {
  type        = bool
  description = "Whether the Service Bus namespace permits public network access."
  default     = false
}

variable "max_delivery_count" {
  type        = number
  description = "Maximum delivery attempts before a message is dead-lettered."
  default     = 10
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to Service Bus resources."
  default     = {}
}