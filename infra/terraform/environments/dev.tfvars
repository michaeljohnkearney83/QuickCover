# Dev: smallest sensible sizes. The subscription comes from ARM_SUBSCRIPTION_ID
# (or a git-ignored *.local.tfvars), so it is never committed.
environment = "dev"

# Sized for an Azure Free Trial subscription, which (as checked on 2026-11-24):
#  - offers no VM sizes in UK South, so dev runs in UK West (still UK data residency);
#  - has a 4 vCPU regional quota. One 2-vCPU node plus the 1-node upgrade surge uses exactly 4,
#    so the pool is pinned at 1 node. On pay-as-you-go, raise max_count (and request more quota if needed).
location = "ukwest"

aks_node_vm_size   = "Standard_D2s_v5"
aks_node_min_count = 1
aks_node_max_count = 1

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
