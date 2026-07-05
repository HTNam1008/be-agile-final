param(
    [string]$FrontendRepositoryPath = "..\aglie-final-brian-fe"
)

$ErrorActionPreference = "Stop"

function Get-TextFiles([string]$Root, [string[]]$RelativePaths) {
    foreach ($relativePath in $RelativePaths) {
        $path = Join-Path $Root $relativePath
        if (-not (Test-Path -LiteralPath $path)) {
            continue
        }

        $item = Get-Item -LiteralPath $path
        if ($item.PSIsContainer) {
            Get-ChildItem -LiteralPath $path -Recurse -File |
                Where-Object {
                    $_.FullName -notmatch "\\(bin|obj|dist|node_modules|\.git|\.verification)\\"
                }
        } else {
            $item
        }
    }
}

function Test-FilePatterns([System.IO.FileInfo]$File, [hashtable]$Patterns, [System.Collections.Generic.List[object]]$Findings) {
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $File.FullName) {
        $lineNumber++
        foreach ($name in $Patterns.Keys) {
            if ($line -match $Patterns[$name]) {
                $Findings.Add([pscustomobject]@{
                    File = $File.FullName
                    Line = $lineNumber
                    Pattern = $name
                })
            }
        }
    }
}

$backendRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
$frontendRoot = if (Test-Path -LiteralPath $FrontendRepositoryPath) {
    (Resolve-Path -LiteralPath $FrontendRepositoryPath).Path
} else {
    $FrontendRepositoryPath
}

$highConfidenceSecretPatterns = @{
    "stripe-secret-key" = "sk_(test|live)_[A-Za-z0-9]{20,}"
    "stripe-webhook-secret" = "whsec_[A-Za-z0-9]{20,}"
    "azure-storage-account-key" = "AccountKey=(?!<)[A-Za-z0-9+/=]{40,}"
    "connection-string-password" = "password=(?!<)[A-Za-z0-9+/=]{12,}"
    "json-client-secret" = '"ClientSecret"\s*:\s*"(?!<)[^"]{12,}"'
    "json-token-signing-key" = '"LocalTokenSigningKey"\s*:\s*"(?!<)[^"]{20,}"'
}

$findings = New-Object System.Collections.Generic.List[object]

$backendFiles = Get-TextFiles $backendRoot @(
    "src\Hosts\Moe.StudentFinance.Api\appsettings.json",
    "src\Hosts\Moe.StudentFinance.Api\appsettings.UAT.json",
    ".github",
    "docs\UAT_AZURE_APP_SERVICE_DEPLOYMENT.md",
    "docs\UAT_DEPLOYMENT_RUNBOOK.md",
    "docs\UAT_APPLICATION_INSIGHTS_QUERIES.md",
    "docs\uat-app-service-settings.template.json",
    "docs\uat-github-settings.md",
    "scripts\azure"
)

$frontendFiles = Get-TextFiles $frontendRoot @(
    ".env.production",
    ".github",
    "docs\UAT_AZURE_STATIC_WEB_APPS_DEPLOYMENT.md",
    "docs\uat-github-settings.md",
    "scripts\azure",
    "staticwebapp.config.json"
)

foreach ($file in @($backendFiles) + @($frontendFiles)) {
    Test-FilePatterns $file $highConfidenceSecretPatterns $findings
}

if ($findings.Count -gt 0) {
    Write-Host "UAT repo hygiene check failed. Potential secrets found:"
    $findings | Format-Table -AutoSize
    exit 1
}

Write-Host "UAT repo hygiene check passed. No high-confidence secret patterns found."
