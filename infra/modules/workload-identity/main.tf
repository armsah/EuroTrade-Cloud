resource "azurerm_user_assigned_identity" "this" {
  name                = var.name
  location            = var.location
  resource_group_name = var.resource_group_name

  tags = var.tags
}

resource "azurerm_federated_identity_credential" "this" {
  name = var.federated_credential_name

  user_assigned_identity_id = azurerm_user_assigned_identity.this.id

  audience = [
    "api://AzureADTokenExchange"
  ]

  issuer = var.oidc_issuer_url

  subject = "system:serviceaccount:${var.kubernetes_namespace}:${var.service_account_name}"
}