param(
    [Parameter(Mandatory = $true)]
    [string]$ApiBaseUrl,

    [string]$FrontendBaseUrl,

    [switch]$RequireSwagger
)

$ErrorActionPreference = "Stop"

function Join-Url([string]$BaseUrl, [string]$Path) {
    return $BaseUrl.TrimEnd("/") + "/" + $Path.TrimStart("/")
}

function Test-HttpOk([string]$Name, [string]$Url) {
    Write-Host "Checking ${Name}: $Url"
    $response = Invoke-WebRequest -Uri $Url -Method Get -MaximumRedirection 5 -TimeoutSec 30
    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        throw "$Name returned HTTP $($response.StatusCode)."
    }

    return $response
}

function Invoke-HttpProbe([string]$Url, [string]$Method = "Get") {
    try {
        return Invoke-WebRequest -Uri $Url -Method $Method -MaximumRedirection 0 -TimeoutSec 30
    }
    catch [Microsoft.PowerShell.Commands.HttpResponseException] {
        return $_.Exception.Response
    }
    catch [System.Net.WebException] {
        if ($_.Exception.Response) {
            return $_.Exception.Response
        }

        throw
    }
}

function Get-StatusCode($Response) {
    return [int]$Response.StatusCode
}

function Assert-CorrelationIdHeader([string]$Name, $Response) {
    $correlationId = $Response.Headers["X-Correlation-Id"]
    if ([string]::IsNullOrWhiteSpace($correlationId)) {
        throw "$Name did not include X-Correlation-Id."
    }
}

function Assert-SpaHtml([string]$Name, $Response) {
    $contentType = [string]$Response.Headers["Content-Type"]
    if ($contentType.IndexOf("text/html", [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "$Name returned non-HTML content type: $contentType"
    }

    if (([string]$Response.Content).IndexOf('<div id="root"', [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "$Name did not look like the Vite SPA shell."
    }
}

$liveResponse = Test-HttpOk "API live health" (Join-Url $ApiBaseUrl "/health/live")
Assert-CorrelationIdHeader "API live health" $liveResponse

$readyResponse = Test-HttpOk "API ready health" (Join-Url $ApiBaseUrl "/health/ready")
Assert-CorrelationIdHeader "API ready health" $readyResponse

if ($RequireSwagger) {
    Test-HttpOk "Swagger UI" (Join-Url $ApiBaseUrl "/swagger") | Out-Null
}

$hubUrl = Join-Url $ApiBaseUrl "/hubs/notifications"
Write-Host "Checking SignalR hub endpoint surface: $hubUrl"
$hubResponse = Invoke-HttpProbe -Url $hubUrl -Method Get
$hubStatusCode = Get-StatusCode $hubResponse
if ($hubStatusCode -notin @(400, 401, 405)) {
    throw "SignalR hub returned unexpected HTTP $hubStatusCode. Expected 400, 401, or 405 for an unauthenticated/plain HTTP probe."
}

$negotiateUrl = Join-Url $ApiBaseUrl "/hubs/notifications/negotiate?negotiateVersion=1"
Write-Host "Checking SignalR negotiate auth gate: $negotiateUrl"
$negotiateResponse = Invoke-HttpProbe -Url $negotiateUrl -Method Post
$negotiateStatusCode = Get-StatusCode $negotiateResponse
if ($negotiateStatusCode -notin @(401, 403)) {
    throw "SignalR negotiate returned unexpected HTTP $negotiateStatusCode. Expected 401 or 403 for an unauthenticated probe."
}

if (-not [string]::IsNullOrWhiteSpace($FrontendBaseUrl)) {
    $frontendRootResponse = Test-HttpOk "Frontend root" $FrontendBaseUrl
    Assert-SpaHtml "Frontend root" $frontendRootResponse

    $frontendRouteResponse = Test-HttpOk "Frontend SPA route" (Join-Url $FrontendBaseUrl "/portal/login")
    Assert-SpaHtml "Frontend SPA route" $frontendRouteResponse
}

Write-Host "UAT smoke checks completed."
