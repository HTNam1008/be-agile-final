param(
    [string]$ResourceGroup = "moe-studentfinance-uat",
    [switch]$Force,
    [switch]$NoWait
)

$ErrorActionPreference = "Stop"

function Require-AzCli {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI is required. Install it from https://learn.microsoft.com/cli/azure/install-azure-cli"
    }

    az account show --only-show-errors | Out-Null
}

Require-AzCli

$exists = az group exists --name $ResourceGroup --only-show-errors
if ($exists -ne "true") {
    Write-Host "Resource group does not exist: $ResourceGroup"
    exit 0
}

Write-Host "This will delete the UAT resource group and all resources inside it:"
Write-Host "  $ResourceGroup"
Write-Host ""
Write-Host "This stops ongoing App Service Plan, Storage, SQL, Log Analytics, and Application Insights cost for this UAT environment."

if (-not $Force) {
    $answer = Read-Host "Type the resource group name to confirm deletion"
    if ($answer -ne $ResourceGroup) {
        Write-Host "Teardown cancelled."
        exit 0
    }
}

$arguments = @("group", "delete", "--name", $ResourceGroup, "--yes", "--only-show-errors")
if ($NoWait) {
    $arguments += "--no-wait"
}

az @arguments

if ($LASTEXITCODE -ne 0) {
    throw "Azure resource group deletion failed with exit code $LASTEXITCODE."
}

if ($NoWait) {
    Write-Host "UAT resource group deletion started: $ResourceGroup"
} else {
    Write-Host "UAT resource group deleted: $ResourceGroup"
}
