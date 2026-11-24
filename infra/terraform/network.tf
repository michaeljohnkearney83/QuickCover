# The VNet exists from day one because moving an AKS cluster to a different subnet means rebuilding it.

resource "azurerm_virtual_network" "main" {
  name                = "vnet-${local.name}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  address_space       = [var.address_space]
  tags                = local.tags
}

# AKS nodes. Pods get overlay IPs (pod_cidr in aks.tf), so this only needs room for nodes.
resource "azurerm_subnet" "aks" {
  name                 = "snet-aks"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = [cidrsubnet(var.address_space, 6, 0)] # /22 from a /16
}

# Reserved for step 6: private endpoints for SQL, DocumentDB, Redis, Storage and Key Vault.
resource "azurerm_subnet" "private_endpoints" {
  name                 = "snet-private-endpoints"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = [cidrsubnet(var.address_space, 8, 16)] # /24
}
