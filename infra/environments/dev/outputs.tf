output "resource_group_name" {
  value = module.resource_group.name
}

output "acr_name" {
  value = module.acr.name
}

output "acr_login_server" {
  value = module.acr.login_server
}

output "key_vault_name" {
  value = module.key_vault.name
}

output "key_vault_uri" {
  value = module.key_vault.uri
}

output "log_analytics_workspace_name" {
  value = module.monitoring.name
}

output "postgresql_server_name" {
  value = module.postgresql.name
}

output "postgresql_fqdn" {
  value = module.postgresql.fqdn
}

output "postgresql_database_name" {
  value = module.postgresql.database_name
}

output "aks_name" {
  value = module.aks.name
}

output "service_bus_namespace_name" {
  value = module.service_bus.name
}

output "service_bus_fully_qualified_namespace" {
  value = module.service_bus.fully_qualified_namespace
}

output "service_bus_queue_name" {
  value = module.service_bus.queue_name
}

output "workload_identity_name" {
  value = module.workload_identity.name
}

output "workload_identity_client_id" {
  value = module.workload_identity.client_id
}

output "application_insights_name" {
  value = module.monitoring.application_insights_name
}

output "application_insights_connection_string" {
  value     = module.monitoring.application_insights_connection_string
  sensitive = true
}

output "kubernetes_namespace" {
  value = var.kubernetes_namespace
}

output "kubernetes_service_account_name" {
  value = var.kubernetes_service_account_name
}