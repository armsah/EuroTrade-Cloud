output "id" {
  description = "Service Bus namespace resource ID."
  value       = azurerm_servicebus_namespace.this.id
}

output "name" {
  description = "Service Bus namespace name."
  value       = azurerm_servicebus_namespace.this.name
}

output "fully_qualified_namespace" {
  description = "Fully qualified Service Bus namespace hostname."
  value       = "${azurerm_servicebus_namespace.this.name}.servicebus.windows.net"
}

output "queue_id" {
  description = "Service Bus queue resource ID."
  value       = azurerm_servicebus_queue.this.id
}

output "queue_name" {
  description = "Service Bus queue name."
  value       = azurerm_servicebus_queue.this.name
}