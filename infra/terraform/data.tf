# Step 4: public endpoints restricted by firewall rules. Step 6 replaces them with private endpoints
# and turns public network access off.

# --- Azure SQL: purchases, policies, outbox ---------------------------------------------------

resource "random_password" "sql_admin" {
  length           = 32
  special          = true
  override_special = "!#%*()-_=+[]{}:?" # nothing that needs escaping in a connection string
}

resource "azurerm_mssql_server" "main" {
  name                          = "sql-${local.name}-${local.suffix}"
  location                      = azurerm_resource_group.main.location
  resource_group_name           = azurerm_resource_group.main.name
  version                       = "12.0"
  minimum_tls_version           = "1.2"
  public_network_access_enabled = true
  tags                          = local.tags

  # SQL login for the apps until step 6 (workload identity + Entra-only auth).
  administrator_login          = "quickcover_admin"
  administrator_login_password = random_password.sql_admin.result

  # Whoever runs Terraform is the Entra admin, so people sign in with Entra, not the SQL password.
  azuread_administrator {
    login_username = "terraform-deployer"
    object_id      = data.azurerm_client_config.current.object_id
    tenant_id      = data.azurerm_client_config.current.tenant_id
  }
}

resource "azurerm_mssql_database" "main" {
  name                        = "QuickCover"
  server_id                   = azurerm_mssql_server.main.id
  sku_name                    = var.sql_sku_name
  max_size_gb                 = 32
  min_capacity                = 0.5
  auto_pause_delay_in_minutes = var.sql_auto_pause_delay_minutes
  zone_redundant              = false
  storage_account_type        = "Local"
  tags                        = local.tags

  short_term_retention_policy {
    retention_days = 7
  }
}

# 0.0.0.0 - 0.0.0.0 is Azure's special "allow Azure services" rule, which lets AKS egress in.
# It also admits other tenants' Azure resources, which is why step 6 moves to Private Link.
resource "azurerm_mssql_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# --- Azure DocumentDB (MongoDB vCore): quotes --------------------------------------------------

resource "random_password" "documentdb_admin" {
  length           = 32
  special          = true
  override_special = "-_." # URL-safe: it goes inside a mongodb+srv:// URI
}

resource "azurerm_mongo_cluster" "main" {
  name                   = "docdb-${local.name}-${local.suffix}"
  location               = azurerm_resource_group.main.location
  resource_group_name    = azurerm_resource_group.main.name
  administrator_username = "quickcover"
  administrator_password = random_password.documentdb_admin.result
  compute_tier           = var.documentdb_compute_tier
  storage_size_in_gb     = var.documentdb_storage_gb
  shard_count            = 1
  high_availability_mode = "Disabled" # dev; ZoneRedundantPreferred for prod
  public_network_access  = "Enabled"
  version                = "8.0"
  tags                   = local.tags
}

resource "azurerm_mongo_cluster_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  mongo_cluster_id = azurerm_mongo_cluster.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# --- Azure Managed Redis: rating-factor cache --------------------------------------------------

resource "azurerm_managed_redis" "main" {
  name                      = "redis-${local.name}-${local.suffix}"
  location                  = azurerm_resource_group.main.location
  resource_group_name       = azurerm_resource_group.main.name
  sku_name                  = var.redis_sku_name
  high_availability_enabled = false # dev; the app already survives a Redis outage
  public_network_access     = "Enabled"
  tags                      = local.tags

  default_database {
    # Access keys until step 6 switches the app to Entra auth.
    access_keys_authentication_enabled = true
    client_protocol                    = "Encrypted"
    clustering_policy                  = "OSSCluster"
    eviction_policy                    = "VolatileLRU"
  }
}

# --- Storage: policy PDFs ---------------------------------------------------------------------

resource "azurerm_storage_account" "documents" {
  name                     = "st${local.compact_name}"
  location                 = azurerm_resource_group.main.location
  resource_group_name      = azurerm_resource_group.main.name
  account_kind             = "StorageV2"
  account_tier             = "Standard"
  account_replication_type = "LRS" # dev; ZRS or GZRS for prod
  min_tls_version          = "TLS1_2"
  public_network_access    = "Enabled"
  tags                     = local.tags

  # Nothing is ever public. Documents are served by the API after an ownership check.
  allow_nested_items_to_be_public = false
  # Still on for the docs worker's connection string. Step 6 switches it to workload identity and turns this off.
  shared_access_key_enabled       = true
  default_to_oauth_authentication = true

  blob_properties {
    versioning_enabled = true
    delete_retention_policy {
      days = 7
    }
    container_delete_retention_policy {
      days = 7
    }
  }
}

resource "azurerm_storage_container" "policy_documents" {
  name                  = "policy-documents"
  storage_account_id    = azurerm_storage_account.documents.id
  container_access_type = "private"
}
