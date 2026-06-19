param(
    [string]$Server = "localhost",
    [string]$Database = "MoeStudentFinance",
    [string]$User,
    [string]$Password,
    [string]$SqlFile = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$defaultSqlFile = Join-Path $repoRoot "docs\database\seed-local-development.sql"
$seedFile = if ([string]::IsNullOrWhiteSpace($SqlFile)) { $defaultSqlFile } else { $SqlFile }

if (-not (Test-Path -LiteralPath $seedFile)) {
    throw "Seed SQL file not found: $seedFile"
}

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    throw "sqlcmd was not found. Install Microsoft sqlcmd tools or run docs\database\seed-local-development.sql manually."
}

$args = @(
    "-S", $Server,
    "-d", $Database,
    "-b",
    "-i", $seedFile
)

if (-not [string]::IsNullOrWhiteSpace($User)) {
    $args += @("-U", $User)

    if ([string]::IsNullOrWhiteSpace($Password)) {
        throw "Password is required when User is supplied."
    }

    $args += @("-P", $Password)
} else {
    $args += "-E"
}

Write-Host "Seeding local MOE Student Finance database..."
Write-Host "Server:   $Server"
Write-Host "Database: $Database"
Write-Host "SQL:      $seedFile"

& $sqlcmd.Source @args

if ($LASTEXITCODE -ne 0) {
    throw "Local database seed failed with exit code $LASTEXITCODE."
}

Write-Host "Local database seed completed."
