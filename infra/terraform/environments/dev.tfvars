# Dev: smallest sensible sizes. The subscription comes from ARM_SUBSCRIPTION_ID
# (or a git-ignored *.local.tfvars), so it is never committed.
environment = "dev"
location    = "uksouth"

aks_node_vm_size   = "Standard_D2s_v5"
aks_node_min_count = 1
aks_node_max_count = 3

# Lock the AKS API server to your own IP, e.g. ["203.0.113.10/32"]. Empty = open (Entra sign-in still required).
aks_api_authorized_ip_ranges = []

# Entra group(s) whose members get cluster-admin. Without one, grant yourself
# "Azure Kubernetes Service RBAC Cluster Admin" on the cluster after apply.
aks_admin_group_object_ids = []

sql_sku_name                 = "GP_S_Gen5_1"
sql_auto_pause_delay_minutes = 60

documentdb_compute_tier = "M10"
documentdb_storage_gb   = 32

redis_sku_name = "Balanced_B0"
