output "key_vault_private_endpoint_id" {
  description = "Key Vault private endpoint resource ID."
  value       = azurerm_private_endpoint.key_vault.id
}

output "postgresql_private_endpoint_id" {
  description = "PostgreSQL private endpoint resource ID."
  value       = azurerm_private_endpoint.postgresql.id
}

output "key_vault_private_dns_zone_id" {
  description = "Key Vault private DNS zone ID."
  value       = azurerm_private_dns_zone.key_vault.id
}

output "postgresql_private_dns_zone_id" {
  description = "PostgreSQL private DNS zone ID."
  value       = azurerm_private_dns_zone.postgresql.id
}

output "service_bus_private_endpoint_id" {
  description = "Service Bus private endpoint resource ID."
  value       = azurerm_private_endpoint.service_bus.id
}

output "service_bus_private_dns_zone_id" {
  description = "Service Bus private DNS zone resource ID."
  value       = azurerm_private_dns_zone.service_bus.id
}