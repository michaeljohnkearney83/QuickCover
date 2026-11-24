# QuickCover infrastructure (Terraform)

Azure resources for one QuickCover environment. Uses Terraform ≥ 1.10 with the azurerm 5.x provider.

| File | What it creates |
|---|---|
| `main.tf` | Resource group, naming, tags |
| `network.tf` | VNet, AKS subnet, a subnet reserved for private endpoints (step 6) |
| `aks.tf` | AKS (Azure CNI overlay + Cilium, Entra ID + Azure RBAC, workload identity, Key Vault CSI driver), ACR, role assignments |
| `data.tf` | Azure SQL (serverless), Azure DocumentDB (MongoDB vCore), Azure Managed Redis, Storage account + private `policy-documents` container |
| `keyvault.tf` | Key Vault (RBAC mode) holding the four app connection strings |
| `outputs.tf` | Names, URLs and the `az aks get-credentials` command |

RabbitMQ isn't here. It runs inside AKS and is installed with Helm in step 5.

## Deploy

Running `apply` creates billable resources. The biggest costs in dev are the AKS node(s) and DocumentDB. SQL
auto-pauses after 60 idle minutes. Run `terraform destroy` when you're done.

```powershell
az login
az account set --subscription <subscription-id>
cd infra/terraform

# Once per subscription: storage account for remote state. Writes environments/dev.backend.hcl.
./scripts/bootstrap-state.ps1 -Environment dev

$env:ARM_SUBSCRIPTION_ID = (az account show --query id -o tsv)
terraform init -backend-config=environments/dev.backend.hcl
terraform plan -var-file=environments/dev.tfvars -out=dev.tfplan
terraform apply dev.tfplan
```

Then use the cluster:

```powershell
terraform output -raw aks_get_credentials | Invoke-Expression
kubelogin convert-kubeconfig -l azurecli
kubectl get nodes
```

Tear down:

```powershell
terraform destroy -var-file=environments/dev.tfvars
```

## Notes

- **Secrets end up in state.** The generated SQL and DocumentDB passwords and the Redis and Storage keys are in
  Terraform state as well as Key Vault. State lives in a private, Entra-only storage account with versioning on.
  Step 6 removes most of these: the apps switch to workload identity and the key-based auth gets turned off.
- **Key Vault secret names** use `--`, for example `ConnectionStrings--Sql`. The .NET Key Vault configuration provider
  maps that to `ConnectionStrings:Sql`, so app config keys don't change between Docker and Azure.
- **First apply can hit a 403 on Key Vault secrets.** The deployer's "Key Vault Secrets Officer" role can take a minute
  to apply. Run `terraform apply` again.
- **Firewalls (temporary):** SQL and DocumentDB allow "Azure services" (`0.0.0.0`) so AKS can reach them over public
  endpoints. That also admits other tenants' Azure resources, which is why step 6 moves everything behind
  Private Link and disables public access.
- **Kubernetes access:** local accounts are disabled. The deployer gets "Azure Kubernetes Service RBAC Cluster Admin".
  Set `aks_admin_group_object_ids` to grant an Entra group instead.
- Keep `.terraform.lock.hcl` committed. It pins provider versions and holds checksums for Windows, Linux and macOS.
  After changing providers, refresh it with:
  `terraform providers lock -platform=windows_amd64 -platform=linux_amd64 -platform=darwin_arm64`.
