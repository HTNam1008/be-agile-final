param(
    [string]$MigrationScriptPath = "artifacts/sql/migrate-uat.sql",

    [Parameter(Mandatory = $true)]
    [string]$SqlServerName,

    [Parameter(Mandatory = $true)]
    [string]$DatabaseName,

    [Parameter(Mandatory = $true)]
    [string]$SqlUser,

    [Parameter(Mandatory = $true)]
    [securestring]$SqlPassword,

    [int]$QueryTimeoutSeconds = 120,

    [switch]$Force
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw "sqlcmd is required. Install Microsoft ODBC Driver/sqlcmd or use Azure Data Studio to run the migration script manually."
}

if (-not (Test-Path -LiteralPath $MigrationScriptPath)) {
    throw "Migration script not found: $MigrationScriptPath"
}

$resolvedScript = (Resolve-Path -LiteralPath $MigrationScriptPath).Path
$scriptInfo = Get-Item -LiteralPath $resolvedScript
if ($scriptInfo.Length -le 0) {
    throw "Migration script is empty: $resolvedScript"
}

$scriptText = Get-Content -Raw -LiteralPath $resolvedScript
if ($scriptText -notmatch "__EFMigrationsHistory") {
    Write-Warning "The script does not reference __EFMigrationsHistory. Confirm it is an EF Core idempotent migration script."
}

$server = $SqlServerName
if ($server -notmatch "\.database\.windows\.net$") {
    $server = "$server.database.windows.net"
}

Write-Host "Target SQL Server: $server"
Write-Host "Target database: $DatabaseName"
Write-Host "Migration script: $resolvedScript"

if (-not $Force) {
    $answer = Read-Host "Apply this migration to UAT? Type APPLY to continue"
    if ($answer -ne "APPLY") {
        Write-Host "Migration cancelled."
        exit 0
    }
}

$credential = [System.Net.NetworkCredential]::new("", $SqlPassword)
$plainPassword = $credential.Password

try {
    sqlcmd `
        -S "tcp:$server,1433" `
        -d $DatabaseName `
        -U $SqlUser `
        -P $plainPassword `
        -i $resolvedScript `
        -b `
        -I `
        -l 30 `
        -t $QueryTimeoutSeconds
}
finally {
    $plainPassword = $null
}

if ($LASTEXITCODE -ne 0) {
    throw "sqlcmd failed with exit code $LASTEXITCODE."
}

Write-Host "UAT migration applied successfully."
