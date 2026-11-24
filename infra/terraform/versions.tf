terraform {
  required_version = ">= 1.10"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 5.15"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.9"
    }
  }

  # Remote state in Azure Storage, locked with blob leases. The settings come from
  # environments/<env>.backend.hcl at init time:
  #   terraform init -backend-config=environments/dev.backend.hcl
  # Create the state storage account once with scripts/bootstrap-state.ps1.
  backend "azurerm" {}
}

provider "azurerm" {
  features {
    key_vault {
      # Dev convenience: `terraform destroy` really removes secrets so names can be reused.
      purge_soft_deleted_secrets_on_destroy = true
    }
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
  }

  subscription_id = var.subscription_id

  # Terraform reads/writes blob data with Entra ID, not account keys.
  storage_use_azuread = true
}
