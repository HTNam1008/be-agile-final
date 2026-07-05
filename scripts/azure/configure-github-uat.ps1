param(
    [Parameter(Mandatory = $true)]
    [string]$BackendRepository,

    [Parameter(Mandatory = $true)]
    [string]$FrontendRepository,

    [string]$ApiAppName = "app-moe-studentfinance-api-uat",
    [string]$ApiBaseUrl = "https://app-moe-studentfinance-api-uat.azurewebsites.net",
    [string]$FrontendBaseUrl = "https://swa-moe-studentfinance-fe-uat.azurestaticapps.net",
    [string]$MsalClientId = "<uat-entra-client-id>",
    [string]$MsalTenantId = "<uat-tenant-id>",
    [string]$EntraUserDomain = "moestudentfinance.onmicrosoft.com",
    [string]$DefaultOrganizationId = "2"
)

$ErrorActionPreference = "Stop"

function Require-GhCli {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI is required. Install it from https://cli.github.com/ and run gh auth login."
    }

    gh auth status | Out-Null
}

function Set-RepoVariable([string]$Repository, [string]$Name, [string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.StartsWith("<", [StringComparison]::Ordinal)) {
        Write-Warning "Skipping $Repository variable $Name because the value is empty or still a placeholder."
        return
    }

    gh variable set $Name --repo $Repository --body $Value | Out-Null
    Write-Host "Set $Repository variable $Name"
}

Require-GhCli

Set-RepoVariable $BackendRepository "AZURE_WEBAPP_NAME" $ApiAppName
Set-RepoVariable $BackendRepository "UAT_API_BASE_URL" $ApiBaseUrl
Set-RepoVariable $BackendRepository "UAT_FRONTEND_BASE_URL" $FrontendBaseUrl

Set-RepoVariable $FrontendRepository "VITE_API_BASE_URL" $ApiBaseUrl
Set-RepoVariable $FrontendRepository "VITE_MSAL_CLIENT_ID" $MsalClientId
Set-RepoVariable $FrontendRepository "VITE_MSAL_TENANT_ID" $MsalTenantId
Set-RepoVariable $FrontendRepository "VITE_ENTRA_USER_DOMAIN" $EntraUserDomain
Set-RepoVariable $FrontendRepository "VITE_HITPAY_REDIRECT_BASE" $FrontendBaseUrl
Set-RepoVariable $FrontendRepository "VITE_CHATBOT_ENDPOINT" "$ApiBaseUrl/api/chatbot"
Set-RepoVariable $FrontendRepository "VITE_ENABLE_DEV_CLOCK" "false"
Set-RepoVariable $FrontendRepository "VITE_DEFAULT_ORGANIZATION_ID" $DefaultOrganizationId

Write-Host ""
Write-Host "Set these GitHub secrets manually:"
Write-Host "- ${BackendRepository}: AZURE_WEBAPP_PUBLISH_PROFILE"
Write-Host "- ${FrontendRepository}: AZURE_STATIC_WEB_APPS_API_TOKEN"
