output "id" {
  description = "Managed identity resource ID."
  value       = azurerm_user_assigned_identity.this.id
}

output "client_id" {
  description = "Managed identity client ID."
  value       = azurerm_user_assigned_identity.this.client_id
}

output "principal_id" {
  description = "Managed identity principal/object ID."
  value       = azurerm_user_assigned_identity.this.principal_id
}

output "name" {
  description = "Managed identity name."
  value       = azurerm_user_assigned_identity.this.name
}