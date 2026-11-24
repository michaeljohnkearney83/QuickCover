resource "azurerm_container_registry" "main" {
  name                = "acr${local.compact_name}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "Basic"
  admin_enabled       = false # pulls use the kubelet identity, pushes use Entra (CI in step 5)
  tags                = local.tags
}

resource "azurerm_kubernetes_cluster" "main" {
  name                = "aks-${local.name}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  dns_prefix          = local.name
  kubernetes_version  = var.aks_kubernetes_version
  sku_tier            = "Free" # no uptime SLA; use Standard for anything customer-facing
  tags                = local.tags

  automatic_upgrade_channel = "patch"
  node_os_upgrade_channel   = "NodeImage"

  # Entra ID for people, Azure RBAC for permissions, no local admin account.
  local_account_disabled            = true
  role_based_access_control_enabled = true
  azure_active_directory_role_based_access_control {
    azure_rbac_enabled     = true
    tenant_id              = data.azurerm_client_config.current.tenant_id
    admin_group_object_ids = var.aks_admin_group_object_ids
  }

  # Workload identity: pods get Entra tokens without secrets. Turned on now, used in step 6.
  oidc_issuer_enabled       = true
  workload_identity_enabled = true

  # Secrets Store CSI driver: mounts Key Vault secrets into pods.
  key_vault_secrets_provider {
    secret_rotation_enabled = true
  }

  dynamic "api_server_access_profile" {
    for_each = length(var.aks_api_authorized_ip_ranges) > 0 ? [1] : []
    content {
      authorized_ip_ranges = var.aks_api_authorized_ip_ranges
    }
  }

  identity {
    type = "SystemAssigned"
  }

  default_node_pool {
    name                         = "system"
    vm_size                      = var.aks_node_vm_size
    vnet_subnet_id               = azurerm_subnet.aks.id
    auto_scaling_enabled         = true
    min_count                    = var.aks_node_min_count
    max_count                    = var.aks_node_max_count
    os_sku                       = "AzureLinux"
    only_critical_addons_enabled = false # dev: apps share the system pool to save cost
    temporary_name_for_rotation  = "systemtmp"

    upgrade_settings {
      max_surge = "1"
    }
  }

  # Node pools are declared above; no node auto-provisioning (Karpenter).
  node_provisioning_profile {
    mode = "Manual"
  }

  network_profile {
    network_plugin      = "azure"
    network_plugin_mode = "overlay"
    network_policy      = "cilium"
    network_data_plane  = "cilium"
    pod_cidr            = "192.168.0.0/16"
    service_cidr        = "172.16.0.0/16"
    dns_service_ip      = "172.16.0.10"
    load_balancer_sku   = "standard"
  }

  lifecycle {
    # Autoscaler changes node_count; patch upgrades change the version. Neither is drift.
    ignore_changes = [
      default_node_pool[0].node_count,
      kubernetes_version,
    ]
  }
}

# Nodes pull images from ACR with the kubelet's managed identity: no registry passwords anywhere.
resource "azurerm_role_assignment" "aks_acr_pull" {
  scope                            = azurerm_container_registry.main.id
  role_definition_name             = "AcrPull"
  principal_id                     = azurerm_kubernetes_cluster.main.kubelet_identity[0].object_id
  principal_type                   = "ServicePrincipal"
  skip_service_principal_aad_check = true
}

# Local accounts are off, so whoever deploys gets cluster-admin through Azure RBAC to be able to use kubectl.
resource "azurerm_role_assignment" "deployer_aks_admin" {
  scope                = azurerm_kubernetes_cluster.main.id
  role_definition_name = "Azure Kubernetes Service RBAC Cluster Admin"
  principal_id         = data.azurerm_client_config.current.object_id
}

# The CSI driver's identity can read secrets (used by the SecretProviderClass in step 5).
resource "azurerm_role_assignment" "aks_kv_secrets_user" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_kubernetes_cluster.main.key_vault_secrets_provider[0].secret_identity[0].object_id
  principal_type       = "ServicePrincipal"
}
