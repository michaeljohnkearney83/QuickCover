<#
.SYNOPSIS
    One-off: creates the storage account that holds Terraform state, then writes environments/<env>.backend.hcl.

.DESCRIPTION
    Terraform can't create its own backend, so this runs once per subscription with the Azure CLI.
    The state account is private to Entra ID: no shared keys, no public blobs, versioning and soft delete on,
    so a bad apply or a deleted state file can be recovered. You get "Storage Blob Data Contributor" on it.

    Safe to re-run: existing resources are reused.

.EXAMPLE
    az login
    ./scripts/bootstrap-state.ps1 -Environment dev
#>
param(
    [string] $Environment = "dev",
    [string] $Location = "uksouth",
    [string] $ResourceGroup = "rg-quickcover-tfstate"
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

function Invoke-Az {
    $output = & az @args
    if ($LASTEXITCODE -ne 0) { throw "az $($args -join ' ') failed (exit $LASTEXITCODE)" }
    return $output
}

$subscriptionId = Invoke-Az account show --query id -o tsv
Write-Host "Subscription: $subscriptionId"

Invoke-Az group create --name $ResourceGroup --location $Location --tags app=quickcover managed_by=bootstrap -o none

# Reuse the account if this script has run before; otherwise create one with a random suffix.
$account = Invoke-Az storage account list -g $ResourceGroup --query "[?starts_with(name, 'stqctfstate')].name | [0]" -o tsv
if (-not $account) {
    $suffix = -join ((97..122) + (48..57) | Get-Random -Count 5 | ForEach-Object { [char]$_ })
    $account = "stqctfstate$suffix"
    Write-Host "Creating storage account $account"
    Invoke-Az storage account create `
        --name $account --resource-group $ResourceGroup --location $Location `
        --sku Standard_LRS --kind StorageV2 --min-tls-version TLS1_2 `
        --allow-blob-public-access false --allow-shared-key-access false `
        -o none
} else {
    Write-Host "Reusing storage account $account"
}

Invoke-Az storage account blob-service-properties update `
    --account-name $account --resource-group $ResourceGroup `
    --enable-versioning true --enable-delete-retention true --delete-retention-days 30 -o none

$accountId = Invoke-Az storage account show -n $account -g $ResourceGroup --query id -o tsv
$me = Invoke-Az ad signed-in-user show --query id -o tsv
$existing = Invoke-Az role assignment list --assignee $me --scope $accountId --role "Storage Blob Data Contributor" --query "length(@)" -o tsv
if ($existing -eq "0") {
    Invoke-Az role assignment create --assignee-object-id $me --assignee-principal-type User `
        --role "Storage Blob Data Contributor" --scope $accountId -o none
    Write-Host "Granted you Storage Blob Data Contributor (can take a minute to apply)"
}

# Role assignments can take a moment to propagate before the data-plane call works.
for ($i = 1; $i -le 10; $i++) {
    & az storage container create --name tfstate --account-name $account --auth-mode login -o none 2>$null
    if ($LASTEXITCODE -eq 0) { break }
    Write-Host "Waiting for permissions to propagate ($i/10)..."
    Start-Sleep -Seconds 15
}
if ($LASTEXITCODE -ne 0) { throw "Could not create the tfstate container." }

$backendFile = Join-Path $PSScriptRoot "..\environments\$Environment.backend.hcl"
@"
resource_group_name  = "$ResourceGroup"
storage_account_name = "$account"
container_name       = "tfstate"
key                  = "quickcover-$Environment.tfstate"
use_azuread_auth     = true
"@ | Set-Content -Path $backendFile -Encoding utf8

Write-Host ""
Write-Host "Wrote $backendFile. Next:"
Write-Host "  `$env:ARM_SUBSCRIPTION_ID = '$subscriptionId'"
Write-Host "  terraform init -backend-config=environments/$Environment.backend.hcl"
Write-Host "  terraform plan -var-file=environments/$Environment.tfvars -out=$Environment.tfplan"
