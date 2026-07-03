param(
    [string]$Path = "docs/uat-app-service-settings.template.json"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path)) {
    throw "Settings file not found: $Path"
}

$raw = Get-Content -Raw -LiteralPath $Path
$parsed = $raw | ConvertFrom-Json
$settings = @{}
foreach ($property in $parsed.PSObject.Properties) {
    $settings[$property.Name] = $property.Value
}

$requiredKeys = @(
    "ASPNETCORE_ENVIRONMENT",
    "APPLICATIONINSIGHTS_CONNECTION_STRING",
    "ConnectionStrings__MoeDatabase",
    "Portals__AdminAllowedOrigins__0",
    "Portals__EServiceAllowedOrigins__0",
    "Authentication__AdminEntra__Authority",
    "Authentication__AdminEntra__Audience",
    "Authentication__AdminEntra__ClientId",
    "Authentication__AdminEntra__Scopes__0",
    "Authentication__AdminEntra__AllowedTenantId",
    "Authentication__AdminEntra__RequireHttpsMetadata",
    "Authentication__EServiceSingpass__RedirectUri",
    "Authentication__EServiceSingpass__PortalRedirectUri",
    "AzureBlob__ConnectionString",
    "AzureBlob__ContainerName",
    "FasDocuments__AzureBlobConnectionString",
    "FasDocuments__ContainerName",
    "BackgroundJobs__Enabled",
    "UAT__EnableSwagger",
    "Stripe__SecretKey",
    "Stripe__WebhookSecret",
    "Stripe__SuccessUrl",
    "Stripe__CancelUrl",
    "MailDelivery__PortalBaseUrl",
    "MailDelivery__Password",
    "EntraWorkforceDirectory__ClientSecret",
    "AzureOpenAI__ApiKey"
)

$errors = New-Object System.Collections.Generic.List[string]

foreach ($key in $requiredKeys) {
    if (-not $settings.ContainsKey($key)) {
        $errors.Add("Missing required key: $key")
        continue
    }

    $value = [string]$settings[$key]
    if ([string]::IsNullOrWhiteSpace($value)) {
        $errors.Add("Empty value: $key")
        continue
    }

    if ($value -match "<[^>]+>") {
        $errors.Add("Placeholder value remains: $key")
    }

    if ($value -match "localhost|127\.0\.0\.1") {
        $errors.Add("Localhost value is not valid for UAT: $key")
    }
}

if (($settings["ASPNETCORE_ENVIRONMENT"] -as [string]) -ne "UAT") {
    $errors.Add("ASPNETCORE_ENVIRONMENT must be UAT.")
}

foreach ($urlKey in @(
    "Portals__AdminAllowedOrigins__0",
    "Portals__EServiceAllowedOrigins__0",
    "Authentication__AdminEntra__Authority",
    "Authentication__EServiceSingpass__RedirectUri",
    "Authentication__EServiceSingpass__PortalRedirectUri",
    "Stripe__SuccessUrl",
    "Stripe__CancelUrl",
    "MailDelivery__PortalBaseUrl"
)) {
    if ($settings.ContainsKey($urlKey)) {
        $value = [string]$settings[$urlKey]
        if (-not $value.StartsWith("https://", [StringComparison]::OrdinalIgnoreCase)) {
            $errors.Add("UAT URL must use https://: $urlKey")
        }
    }
}

if ($settings.ContainsKey("Authentication__AdminEntra__Authority")) {
    $authority = [string]$settings["Authentication__AdminEntra__Authority"]
    if ($authority -notmatch "^https://login\.microsoftonline\.com/[^/]+/v2\.0/?$") {
        $errors.Add("Authentication__AdminEntra__Authority must be an Entra v2.0 authority URL.")
    }
}

if (($settings["Authentication__AdminEntra__Audience"] -as [string]) -notmatch "^api://") {
    $errors.Add("Authentication__AdminEntra__Audience should start with api://.")
}

if (($settings["Authentication__AdminEntra__Scopes__0"] -as [string]) -notmatch "^api://.+/.+") {
    $errors.Add("Authentication__AdminEntra__Scopes__0 should be a full API scope such as api://<api-client-id>/access_as_admin.")
}

if (($settings["Authentication__AdminEntra__RequireHttpsMetadata"] -as [string]) -ne "true") {
    $errors.Add("Authentication__AdminEntra__RequireHttpsMetadata must be true for UAT.")
}

if ($errors.Count -gt 0) {
    Write-Host "UAT App Service settings validation failed:"
    foreach ($validationError in $errors) {
        Write-Host "- $validationError"
    }
    exit 1
}

Write-Host "UAT App Service settings validation passed: $Path"
