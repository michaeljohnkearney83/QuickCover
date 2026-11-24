output "resource_group_name" {
  value = azurerm_resource_group.main.name
}

output "aks_name" {
  value = azurerm_kubernetes_cluster.main.name
}

output "aks_get_credentials" {
  description = "Run this, then `kubelogin convert-kubeconfig -l azurecli`, to use kubectl."
  value       = "az aks get-credentials -g ${azurerm_resource_group.main.name} -n ${azurerm_kubernetes_cluster.main.name}"
}

output "aks_oidc_issuer_url" {
  description = "For workload identity federated credentials (step 6)."
  value       = azurerm_kubernetes_cluster.main.oidc_issuer_url
}

output "aks_secrets_provider_client_id" {
  description = "Client id for the SecretProviderClass (userAssignedIdentityID) in step 5."
  value       = azurerm_kubernetes_cluster.main.key_vault_secrets_provider[0].secret_identity[0].client_id
}

output "acr_login_server" {
  value = azurerm_container_registry.main.login_server
}

output "key_vault_name" {
  value = azurerm_key_vault.main.name
}

output "key_vault_uri" {
  value = azurerm_key_vault.main.vault_uri
}

output "key_vault_secret_names" {
  description = "Connection strings stored for the apps."
  value       = sort(keys(azurerm_key_vault_secret.app))
}

output "sql_server_fqdn" {
  value = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "storage_account_name" {
  value = azurerm_storage_account.documents.name
}

output "tenant_id" {
  value = data.azurerm_client_config.current.tenant_id
}
