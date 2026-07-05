param(
    [string]$ResourceGroup = "moe-studentfinance-uat",
    [string]$Location = "eastasia",
    [string]$StaticWebAppLocation = "eastasia",
    [string]$AppServicePlan = "asp-moe-studentfinance-uat",
    [string]$ApiAppName = "app-moe-studentfinance-api-uat",
    [string]$StaticWebAppName = "swa-moe-studentfinance-fe-uat",
    [string]$StorageAccountName = "stmoestudentfinanceuat",
    [string]$StorageContainerName = "materials",
    [string]$LogAnalyticsWorkspace = "law-moe-studentfinance-uat",
    [string]$ApplicationInsightsName = "appi-moe-studentfinance-uat",
    [string]$LinuxRuntime = "DOTNETCORE:10.0",
    [int]$AppInsightsDailyCapGb = 1
)

$ErrorActionPreference = "Stop"

function Require-AzCli {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI is required. Install it from https://learn.microsoft.com/cli/azure/install-azure-cli"
    }

    az account show --only-show-errors | Out-Null
}

function Write-Section([string]$Title) {
    Write-Host ""
    Write-Host "== $Title =="
}

function Assert-AzSucceeded([string]$Operation) {
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI operation failed: $Operation"
    }
}

function Assert-LocationAllowedByPolicy([string[]]$Locations) {
    $assignmentsJson = az policy assignment list --query "[?name=='sys.regionrestriction'].parameters.listOfAllowedLocations.value | [0]" --output json --only-show-errors
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($assignmentsJson) -or $assignmentsJson.Trim() -eq "null") {
        Write-Warning "Could not read Azure region restriction policy. Continuing without policy pre-check."
        return
    }

    $allowedLocations = $assignmentsJson | ConvertFrom-Json
    foreach ($locationToCheck in $Locations) {
        if ($locationToCheck -notin $allowedLocations) {
            throw "Azure policy does not allow region '$locationToCheck'. Allowed regions: $($allowedLocations -join ', '). Re-run with -Location <allowed-region> and -StaticWebAppLocation <allowed-region>."
        }
    }
}

function Ensure-ResourceGroup([string]$Name, [string]$DesiredLocation) {
    $groupExists = az group exists `
        --name $Name `
        --output tsv `
        --only-show-errors
    Assert-AzSucceeded "check whether resource group $Name exists"

    if ([string]::Equals($groupExists.Trim(), "true", [StringComparison]::OrdinalIgnoreCase)) {
        $existingLocation = az group show `
            --name $Name `
            --query location `
            --output tsv `
            --only-show-errors
        Assert-AzSucceeded "read resource group $Name location"

        if (-not [string]::Equals($existingLocation.Trim(), $DesiredLocation, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Resource group '$Name' already exists in '$existingLocation', but this UAT deployment is configured for '$DesiredLocation'. Azure cannot move a resource group to another region. Delete the old group with: az group delete --name $Name --yes --no-wait ; or re-run provision-uat.ps1 with a new -ResourceGroup name."
        }

        Write-Host "Resource group '$Name' already exists in '$existingLocation'. Reusing it."
        return
    }

    az group create `
        --name $Name `
        --location $DesiredLocation `
        --only-show-errors | Out-Null
    Assert-AzSucceeded "create resource group $Name"
}

function Try-SetLogAnalyticsDailyQuota([string]$ResourceGroupName, [string]$WorkspaceName, [int]$DailyCapGb) {
    if ($DailyCapGb -le 0) {
        Write-Host "Skipping Log Analytics daily quota because AppInsightsDailyCapGb is $DailyCapGb."
        return
    }

    az monitor log-analytics workspace update `
        --resource-group $ResourceGroupName `
        --workspace-name $WorkspaceName `
        --quota $DailyCapGb `
        --only-show-errors | Out-Null

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Set Log Analytics daily quota to $DailyCapGb GB."
        return
    }

    Write-Warning "Could not set Log Analytics daily quota. Continue provisioning and set the daily cap manually in Azure Portal if needed."
}

function Set-WebAppSettingsFromObject([string]$ResourceGroupName, [string]$WebAppName, [hashtable]$Settings, [string]$OperationName) {
    $settingsPath = Join-Path ([System.IO.Path]::GetTempPath()) ("moe-uat-appsettings-" + [Guid]::NewGuid().ToString("N") + ".json")
    try {
        $Settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $settingsPath -Encoding UTF8

        az webapp config appsettings set `
            --name $WebAppName `
            --resource-group $ResourceGroupName `
            --settings "@$settingsPath" `
            --only-show-errors | Out-Null
        Assert-AzSucceeded $OperationName
    }
    finally {
        Remove-Item -LiteralPath $settingsPath -Force -ErrorAction SilentlyContinue
    }
}

Require-AzCli
Assert-LocationAllowedByPolicy @($Location, $StaticWebAppLocation)

Write-Section "Resource group"
Ensure-ResourceGroup $ResourceGroup $Location

Write-Section "Log Analytics + Application Insights"
az monitor log-analytics workspace create `
    --resource-group $ResourceGroup `
    --workspace-name $LogAnalyticsWorkspace `
    --location $Location `
    --only-show-errors | Out-Null
Assert-AzSucceeded "create Log Analytics workspace $LogAnalyticsWorkspace in $Location"

$workspaceId = az monitor log-analytics workspace show `
    --resource-group $ResourceGroup `
    --workspace-name $LogAnalyticsWorkspace `
    --query id `
    --output tsv `
    --only-show-errors
Assert-AzSucceeded "read Log Analytics workspace $LogAnalyticsWorkspace"

az monitor app-insights component create `
    --app $ApplicationInsightsName `
    --location $Location `
    --resource-group $ResourceGroup `
    --workspace $workspaceId `
    --only-show-errors | Out-Null
Assert-AzSucceeded "create Application Insights $ApplicationInsightsName in $Location"

Try-SetLogAnalyticsDailyQuota $ResourceGroup $LogAnalyticsWorkspace $AppInsightsDailyCapGb

$appInsightsConnectionString = az monitor app-insights component show `
    --app $ApplicationInsightsName `
    --resource-group $ResourceGroup `
    --query connectionString `
    --output tsv `
    --only-show-errors
Assert-AzSucceeded "read Application Insights connection string"

Write-Section "Storage"
az storage account create `
    --name $StorageAccountName `
    --resource-group $ResourceGroup `
    --location $Location `
    --sku Standard_LRS `
    --kind StorageV2 `
    --allow-blob-public-access false `
    --min-tls-version TLS1_2 `
    --only-show-errors | Out-Null
Assert-AzSucceeded "create storage account $StorageAccountName in $Location"

$storageConnectionString = az storage account show-connection-string `
    --name $StorageAccountName `
    --resource-group $ResourceGroup `
    --query connectionString `
    --output tsv `
    --only-show-errors
Assert-AzSucceeded "read storage connection string"

az storage container create `
    --name $StorageContainerName `
    --connection-string $storageConnectionString `
    --public-access off `
    --only-show-errors | Out-Null
Assert-AzSucceeded "create storage container $StorageContainerName"

Write-Section "Backend App Service"
az appservice plan create `
    --name $AppServicePlan `
    --resource-group $ResourceGroup `
    --location $Location `
    --sku B1 `
    --is-linux `
    --only-show-errors | Out-Null
Assert-AzSucceeded "create App Service Plan $AppServicePlan in $Location"

az webapp create `
    --name $ApiAppName `
    --resource-group $ResourceGroup `
    --plan $AppServicePlan `
    --runtime $LinuxRuntime `
    --https-only true `
    --only-show-errors | Out-Null
Assert-AzSucceeded "create App Service $ApiAppName"

az webapp config set `
    --name $ApiAppName `
    --resource-group $ResourceGroup `
    --always-on true `
    --web-sockets-enabled true `
    --startup-file "dotnet Moe.StudentFinance.Api.dll" `
    --only-show-errors | Out-Null
Assert-AzSucceeded "configure App Service platform settings"

Set-WebAppSettingsFromObject `
    -ResourceGroupName $ResourceGroup `
    -WebAppName $ApiAppName `
    -OperationName "set initial App Service app settings" `
    -Settings @{
        ASPNETCORE_ENVIRONMENT = "UAT"
        APPLICATIONINSIGHTS_CONNECTION_STRING = $appInsightsConnectionString
        AzureBlob__ConnectionString = $storageConnectionString
        AzureBlob__ContainerName = $StorageContainerName
        FasDocuments__AzureBlobConnectionString = $storageConnectionString
        FasDocuments__ContainerName = $StorageContainerName
        BackgroundJobs__Enabled = "true"
        UAT__EnableSwagger = "true"
    }

Write-Section "Frontend Static Web App"
az extension add --name staticwebapp --upgrade --only-show-errors | Out-Null
Assert-AzSucceeded "install or update staticwebapp extension"
az staticwebapp create `
    --name $StaticWebAppName `
    --resource-group $ResourceGroup `
    --location $StaticWebAppLocation `
    --sku Free `
    --only-show-errors | Out-Null
Assert-AzSucceeded "create Static Web App $StaticWebAppName in $StaticWebAppLocation"

$apiUrl = "https://$ApiAppName.azurewebsites.net"
$staticWebApp = az staticwebapp show `
    --name $StaticWebAppName `
    --resource-group $ResourceGroup `
    --query "defaultHostname" `
    --output tsv `
    --only-show-errors
Assert-AzSucceeded "read Static Web App hostname"
$frontendUrl = "https://$staticWebApp"
$stripeSuccessUrl = "$frontendUrl/portal/payments/return?checkoutId={CHECKOUT_ID}&result=success&session_id={CHECKOUT_SESSION_ID}"
$stripeCancelUrl = "$frontendUrl/portal/payments/return?checkoutId={CHECKOUT_ID}&result=cancelled"

Set-WebAppSettingsFromObject `
    -ResourceGroupName $ResourceGroup `
    -WebAppName $ApiAppName `
    -OperationName "set portal URL App Service app settings" `
    -Settings @{
        Portals__AdminAllowedOrigins__0 = $frontendUrl
        Portals__EServiceAllowedOrigins__0 = $frontendUrl
        Authentication__EServiceSingpass__RedirectUri = "$apiUrl/api/eservice/v1/auth/callback"
        Authentication__EServiceSingpass__PortalRedirectUri = "$frontendUrl/portal/login"
        Stripe__SuccessUrl = $stripeSuccessUrl
        Stripe__CancelUrl = $stripeCancelUrl
        MailDelivery__PortalBaseUrl = $frontendUrl
    }

Write-Section "Manual follow-up"
Write-Host "1. Create or configure Azure SQL Database, preferably with the Azure SQL free offer when available."
Write-Host "2. Set App Service connection string ConnectionStrings__MoeDatabase to the Azure SQL connection string."
Write-Host "3. Rotate and set Stripe, MailDelivery, EntraWorkforceDirectory, AzureOpenAI, and any other secrets."
Write-Host "4. Download the App Service publish profile and store it as GitHub secret AZURE_WEBAPP_PUBLISH_PROFILE."
Write-Host "5. Store AZURE_WEBAPP_NAME as GitHub repository variable: $ApiAppName"
Write-Host "6. Store the Static Web Apps deployment token as GitHub secret AZURE_STATIC_WEB_APPS_API_TOKEN."
Write-Host "   Token command: az staticwebapp secrets list --name $StaticWebAppName --resource-group $ResourceGroup --query properties.apiKey --output tsv"

Write-Section "Frontend GitHub repository variables"
Write-Host "VITE_API_BASE_URL=$apiUrl"
Write-Host "VITE_HITPAY_REDIRECT_BASE=$frontendUrl"
Write-Host "VITE_CHATBOT_ENDPOINT=$apiUrl/api/chatbot"
Write-Host "VITE_ENABLE_DEV_CLOCK=false"
Write-Host "VITE_DEFAULT_ORGANIZATION_ID=2"
Write-Host "VITE_ENTRA_USER_DOMAIN=moestudentfinance.onmicrosoft.com"
Write-Host "VITE_MSAL_CLIENT_ID=<uat-entra-client-id>"
Write-Host "VITE_MSAL_TENANT_ID=<uat-tenant-id>"

Write-Section "Backend URLs"
Write-Host "API: $apiUrl"
Write-Host "Swagger: $apiUrl/swagger"
Write-Host "SignalR: $apiUrl/hubs/notifications"
Write-Host "Frontend: $frontendUrl"
