resource "azurerm_key_vault" "main" {
  name                       = "kv-qc-${var.environment}-${local.suffix}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  rbac_authorization_enabled = true  # Azure RBAC, not legacy access policies
  purge_protection_enabled   = false # dev; turn on for prod (it blocks deleting the vault for 7-90 days)
  soft_delete_retention_days = 7
  tags                       = local.tags
}

# Terraform itself needs to write secrets.
resource "azurerm_role_assignment" "deployer_kv_secrets_officer" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

# Secret names use "--" because the .NET Key Vault configuration provider maps it to ":".
# So "ConnectionStrings--Sql" becomes the same ConnectionStrings:Sql key the apps already read.
locals {
  app_secrets = {
    "ConnectionStrings--Sql" = join(";", [
      "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433",
      "Database=${azurerm_mssql_database.main.name}",
      "User ID=${azurerm_mssql_server.main.administrator_login}",
      "Password=${random_password.sql_admin.result}",
      "Encrypt=True",
      "TrustServerCertificate=False",
      "Connection Timeout=30",
    ])

    # The service returns a URI with placeholder credentials; swap in the real ones.
    "ConnectionStrings--Mongo" = replace(
      azurerm_mongo_cluster.main.connection_strings[0].value,
      "/mongodb\\+srv:\\/\\/[^@]*@/",
      "mongodb+srv://${azurerm_mongo_cluster.main.administrator_username}:${random_password.documentdb_admin.result}@"
    )

    "ConnectionStrings--Redis" = join(",", [
      "${azurerm_managed_redis.main.hostname}:${azurerm_managed_redis.main.default_database[0].port}",
      "password=${azurerm_managed_redis.main.default_database[0].primary_access_key}",
      "ssl=True",
      "abortConnect=False",
    ])

    "ConnectionStrings--Blob" = azurerm_storage_account.documents.primary_blob_connection_string
  }
}

resource "azurerm_key_vault_secret" "app" {
  for_each = nonsensitive(toset(keys(local.app_secrets)))

  name         = each.key
  value        = local.app_secrets[each.key]
  key_vault_id = azurerm_key_vault.main.id
  content_type = "connection-string"

  depends_on = [azurerm_role_assignment.deployer_kv_secrets_officer]
}
