resource "azurerm_role_assignment" "service_bus_sender" {
  scope = module.service_bus.id

  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = module.workload_identity.principal_id
}

resource "azurerm_role_assignment" "service_bus_receiver" {
  scope = module.service_bus.id

  role_definition_name = "Azure Service Bus Data Receiver"
  principal_id         = module.workload_identity.principal_id
}

resource "azurerm_role_assignment" "key_vault_secrets_user" {
  scope = module.key_vault.id

  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.workload_identity.principal_id
}