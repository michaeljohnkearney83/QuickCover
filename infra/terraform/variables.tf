variable "subscription_id" {
  description = "Azure subscription to deploy into. Leave null to use the ARM_SUBSCRIPTION_ID environment variable."
  type        = string
  default     = null
}

variable "environment" {
  description = "Short environment name, used in resource names and tags (dev, test, prod)."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9]{2,6}$", var.environment))
    error_message = "Use 2-6 lowercase letters or digits, e.g. dev."
  }
}

variable "location" {
  description = "Azure region. UK South keeps customer data in the UK."
  type        = string
  default     = "uksouth"
}

variable "address_space" {
  description = "VNet address space. Must not overlap other networks you will peer with."
  type        = string
  default     = "10.40.0.0/16"
}

# --- AKS -------------------------------------------------------------------------------------

variable "aks_kubernetes_version" {
  description = "AKS Kubernetes version. Null uses the region's default; pin it once you care about upgrades."
  type        = string
  default     = null
}

variable "aks_node_vm_size" {
  description = "VM size for the system/user node pool."
  type        = string
  default     = "Standard_D2s_v5"
}

variable "aks_node_min_count" {
  type    = number
  default = 1
}

variable "aks_node_max_count" {
  type    = number
  default = 3
}

variable "aks_api_authorized_ip_ranges" {
  description = "CIDRs allowed to reach the AKS API server. Empty means open to the internet (still needs Entra auth)."
  type        = list(string)
  default     = []
}

variable "aks_admin_group_object_ids" {
  description = "Entra ID groups that get cluster-admin through Azure RBAC."
  type        = list(string)
  default     = []
}

# --- Data services ---------------------------------------------------------------------------

variable "sql_sku_name" {
  description = "Azure SQL database SKU. GP_S_* is serverless and can auto-pause."
  type        = string
  default     = "GP_S_Gen5_1"
}

variable "sql_auto_pause_delay_minutes" {
  description = "Serverless SQL auto-pause after this many idle minutes. -1 disables auto-pause."
  type        = number
  default     = 60
}

variable "documentdb_compute_tier" {
  description = "Azure DocumentDB (MongoDB vCore) compute tier."
  type        = string
  default     = "M10"
}

variable "documentdb_storage_gb" {
  type    = number
  default = 32
}

variable "redis_sku_name" {
  description = "Azure Managed Redis SKU."
  type        = string
  default     = "Balanced_B0"
}

variable "tags" {
  description = "Extra tags for every resource."
  type        = map(string)
  default     = {}
}
