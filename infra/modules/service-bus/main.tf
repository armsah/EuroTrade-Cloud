resource "azurerm_servicebus_namespace" "this" {
  name                = var.name
  location            = var.location
  resource_group_name = var.resource_group_name

  sku      = var.sku
  capacity = var.sku == "Premium" ? var.capacity : 0

  premium_messaging_partitions = var.sku == "Premium" ? 1 : null

  public_network_access_enabled = var.public_network_access_enabled
  local_auth_enabled            = false

  tags = var.tags
}

resource "azurerm_servicebus_queue" "this" {
  name         = var.queue_name
  namespace_id = azurerm_servicebus_namespace.this.id

  max_delivery_count = var.max_delivery_count

  dead_lettering_on_message_expiration = true
}