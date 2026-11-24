# QuickCover on Azure: the cloud equivalents of the docker-compose services.
#
#   docker-compose      ->  Azure
#   sqlserver           ->  Azure SQL Database (serverless)          data.tf
#   mongo               ->  Azure DocumentDB (MongoDB vCore)         data.tf
#   redis               ->  Azure Managed Redis                      data.tf
#   azurite             ->  Storage account, private blob container  data.tf
#   rabbitmq            ->  runs inside AKS (Helm, step 5)
#   quote-api, workers, web  ->  AKS, images in ACR                  aks.tf
#   connection strings  ->  Key Vault                                keyvault.tf
#
# Step 4 uses public endpoints with firewall rules. Step 6 moves the data services behind Private Link
# (the snet-private-endpoints subnet is already reserved) and swaps secrets for workload identity.

data "azurerm_client_config" "current" {}

locals {
  name = "quickcover-${var.environment}"

  # Globally unique names (storage, ACR, Key Vault, SQL, DocumentDB, Redis) get a stable random suffix.
  suffix       = random_string.suffix.result
  compact_name = "qc${var.environment}${local.suffix}" # letters and digits only, for storage and ACR

  tags = merge({
    app         = "quickcover"
    environment = var.environment
    managed_by  = "terraform"
  }, var.tags)
}

resource "random_string" "suffix" {
  length  = 5
  upper   = false
  special = false
}

resource "azurerm_resource_group" "main" {
  name     = "rg-${local.name}"
  location = var.location
  tags     = local.tags
}
