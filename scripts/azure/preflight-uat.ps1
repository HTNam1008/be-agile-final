param(
    [string]$FrontendRepositoryPath = "..\aglie-final-brian-fe",
    [string]$ExpectedBranch = "brian/deploy",
    [switch]$RequireAzureLogin,
    [switch]$RequireGitHubLogin,
    [switch]$RequireSqlcmd
)

$ErrorActionPreference = "Stop"

function Add-CheckResult(
    [System.Collections.Generic.List[object]]$Results,
    [string]$Name,
    [bool]$Passed,
    [string]$Detail)
{
    $Results.Add([pscustomobject]@{
        Name = $Name
        Passed = $Passed
        Detail = $Detail
    })
}

function Test-CommandAvailable([string]$CommandName) {
    return [bool](Get-Command $CommandName -ErrorAction SilentlyContinue)
}

function Test-GitBranch([string]$Path, [string]$Expected) {
    if (-not (Test-Path -LiteralPath $Path)) {
        return "missing"
    }

    $branch = git -C $Path branch --show-current
    if ($LASTEXITCODE -ne 0) {
        return "git-error"
    }

    return $branch.Trim()
}

function Test-RequiredFiles([string]$Root, [string[]]$RelativePaths) {
    $missing = New-Object System.Collections.Generic.List[string]
    foreach ($relativePath in $RelativePaths) {
        $path = Join-Path $Root $relativePath
        if (-not (Test-Path -LiteralPath $path)) {
            $missing.Add($relativePath)
        }
    }

    return $missing
}

$backendRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
$frontendRoot = if (Test-Path -LiteralPath $FrontendRepositoryPath) {
    (Resolve-Path -LiteralPath $FrontendRepositoryPath).Path
} else {
    $FrontendRepositoryPath
}

$results = New-Object System.Collections.Generic.List[object]

foreach ($command in @("git", "dotnet", "node", "npm", "az")) {
    Add-CheckResult $results "tool:$command" (Test-CommandAvailable $command) "Required for UAT deployment workflow."
}

Add-CheckResult $results "tool:gh" (Test-CommandAvailable "gh") "Optional unless using configure-github-uat.ps1 or -RequireGitHubLogin."
Add-CheckResult $results "tool:sqlcmd" (Test-CommandAvailable "sqlcmd") "Optional unless applying Azure SQL migrations from this machine."

if ($RequireAzureLogin -and (Test-CommandAvailable "az")) {
    az account show --only-show-errors | Out-Null
    Add-CheckResult $results "az-login" ($LASTEXITCODE -eq 0) "Azure CLI must be logged in."
}

if ($RequireGitHubLogin -and (Test-CommandAvailable "gh")) {
    gh auth status | Out-Null
    Add-CheckResult $results "gh-login" ($LASTEXITCODE -eq 0) "GitHub CLI must be logged in."
}

$backendBranch = Test-GitBranch $backendRoot $ExpectedBranch
Add-CheckResult $results "backend-branch" ($backendBranch -eq $ExpectedBranch) "Current: $backendBranch"

$frontendBranch = Test-GitBranch $frontendRoot $ExpectedBranch
Add-CheckResult $results "frontend-branch" ($frontendBranch -eq $ExpectedBranch) "Current: $frontendBranch"

$backendRequired = @(
    ".github\workflows\deploy-uat.yml",
    ".github\workflows\smoke-uat.yml",
    "scripts\azure\provision-uat.ps1",
    "scripts\azure\configure-github-uat.ps1",
    "scripts\azure\validate-uat-app-settings.ps1",
    "scripts\azure\apply-uat-migration.ps1",
    "scripts\azure\teardown-uat.ps1",
    "scripts\azure\test-uat.ps1",
    "docs\UAT_DEPLOYMENT_RUNBOOK.md",
    "docs\uat-app-service-settings.template.json"
)
$missingBackend = Test-RequiredFiles $backendRoot $backendRequired
Add-CheckResult $results "backend-files" ($missingBackend.Count -eq 0) (($missingBackend -join ", ") -replace "^$", "All required files present.")

$frontendRequired = @(
    ".github\workflows\deploy-uat.yml",
    "scripts\azure\validate-uat-env.mjs",
    "staticwebapp.config.json",
    "docs\UAT_AZURE_STATIC_WEB_APPS_DEPLOYMENT.md",
    "docs\uat-github-settings.md"
)
$missingFrontend = Test-RequiredFiles $frontendRoot $frontendRequired
Add-CheckResult $results "frontend-files" ($missingFrontend.Count -eq 0) (($missingFrontend -join ", ") -replace "^$", "All required files present.")

$hygieneScript = Join-Path $PSScriptRoot "check-uat-repo-hygiene.ps1"
powershell -NoProfile -ExecutionPolicy Bypass -File $hygieneScript -FrontendRepositoryPath $frontendRoot | Out-Host
Add-CheckResult $results "repo-hygiene" ($LASTEXITCODE -eq 0) "No high-confidence secret patterns found in UAT deploy files."

if ($RequireSqlcmd) {
    $sqlcmd = $results | Where-Object { $_.Name -eq "tool:sqlcmd" } | Select-Object -First 1
    if ($sqlcmd -and -not $sqlcmd.Passed) {
        $sqlcmd.Detail = "sqlcmd is required because -RequireSqlcmd was set."
    }
}

$failed = $results | Where-Object {
    -not $_.Passed
} | Where-Object {
    ($_.Name -ne "tool:sqlcmd" -or $RequireSqlcmd) -and
    ($_.Name -ne "tool:gh" -or $RequireGitHubLogin)
}

$results | Format-Table -AutoSize

if ($failed) {
    Write-Host ""
    Write-Host "UAT preflight failed."
    exit 1
}

Write-Host ""
Write-Host "UAT preflight passed."
