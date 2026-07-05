# Hướng Dẫn Deploy Backend UAT Lên Azure App Service

Backend ASP.NET Core API sẽ chạy trên Azure App Service B1 Linux.

## Azure Resources

- Resource group: `moe-studentfinance-uat`
- Region: mặc định `eastasia` vì Azure for Students policy của subscription đang cho phép region này.
- App Service Plan: `asp-moe-studentfinance-uat`
- App Service: `app-moe-studentfinance-api-uat`
- Azure SQL Database: `sqldb-moe-studentfinance-uat`
- Storage container: `materials`
- Application Insights: `appi-moe-studentfinance-uat`

Dùng B1 Linux vì backend có SignalR và cần WebSocket ổn định hơn Free F1.

## Provision

```powershell
.\scripts\azure\provision-uat.ps1
```

Script tạo skeleton Azure. Azure SQL tạo thủ công trong Portal để chọn free/student offer nếu có.

## GitHub Configuration

Repository variable:

- `AZURE_WEBAPP_NAME=app-moe-studentfinance-api-uat`
- `UAT_API_BASE_URL=https://app-moe-studentfinance-api-uat.azurewebsites.net`
- `UAT_FRONTEND_BASE_URL=https://<static-web-app-hostname>`

Repository secret:

- `AZURE_WEBAPP_PUBLISH_PROFILE`

## App Service Settings

Set trong Azure App Service Configuration:

- `ASPNETCORE_ENVIRONMENT=UAT`
- `APPLICATIONINSIGHTS_CONNECTION_STRING=<Application Insights connection string>`
- `ConnectionStrings__MoeDatabase=<Azure SQL connection string>`
- `Portals__AdminAllowedOrigins__0=<Static Web App URL>`
- `Portals__EServiceAllowedOrigins__0=<Static Web App URL>`
- `Authentication__AdminEntra__Authority=https://login.microsoftonline.com/<uat-tenant-id>/v2.0`
- `Authentication__AdminEntra__Audience=api://<uat-api-app-registration-client-id>`
- `Authentication__AdminEntra__ClientId=<uat-spa-app-registration-client-id>`
- `Authentication__AdminEntra__Scopes__0=api://<uat-api-app-registration-client-id>/access_as_admin`
- `Authentication__AdminEntra__AllowedTenantId=<uat-tenant-id>`
- `Authentication__AdminEntra__RequireHttpsMetadata=true`
- `Authentication__EServiceSingpass__RedirectUri=<API URL>/api/eservice/v1/auth/callback`
- `Authentication__EServiceSingpass__PortalRedirectUri=<Static Web App URL>/portal/login`
- `Stripe__SecretKey=<rotated secret>`
- `Stripe__WebhookSecret=<rotated secret>`
- `Stripe__SuccessUrl=<Static Web App URL>/portal/payments/return?checkoutId={CHECKOUT_ID}&result=success&session_id={CHECKOUT_SESSION_ID}`
- `Stripe__CancelUrl=<Static Web App URL>/portal/payments/return?checkoutId={CHECKOUT_ID}&result=cancelled`
- `MailDelivery__PortalBaseUrl=<Static Web App URL>`
- `MailDelivery__Password=<rotated secret>`
- `MailDelivery__FallbackPassword=<rotated secret>`
- `EntraWorkforceDirectory__ClientSecret=<rotated secret>`
- `AzureBlob__ConnectionString=<storage connection string>`
- `AzureBlob__ContainerName=materials`
- `FasDocuments__AzureBlobConnectionString=<storage connection string>`
- `FasDocuments__ContainerName=materials`
- `BackgroundJobs__Enabled=true`
- `UAT__EnableSwagger=true`

Nên điền từ template:

[uat-app-service-settings.template.json](./uat-app-service-settings.template.json)

Validate trước khi apply:

```powershell
.\scripts\azure\validate-uat-app-settings.ps1 `
  -Path .\uat-app-service-settings.local.json
```

## Platform Toggles

Bật:

- WebSockets: On
- Always On: On
- HTTPS Only: On

## CI/CD Workflow

Workflow:

```text
.github/workflows/deploy-uat.yml
```

Workflow sẽ:

- Restore/build API.
- Publish API artifact.
- Sinh EF migration script idempotent.
- Upload migration artifact.
- Deploy App Service bằng publish profile.
- Có thể chạy smoke test nếu manual input `run_smoke=true`.

## Database Migration

Download artifact `uat-migration-script`, rồi apply `migrate-uat.sql` vào Azure SQL.

```powershell
.\scripts\azure\apply-uat-migration.ps1 `
  -MigrationScriptPath .\migrate-uat.sql `
  -SqlServerName "<azure-sql-server-name>" `
  -DatabaseName "sqldb-moe-studentfinance-uat" `
  -SqlUser "<sql-admin-user>" `
  -SqlPassword (Read-Host "SQL password" -AsSecureString)
```

## Smoke Test

```powershell
.\scripts\azure\test-uat.ps1 `
  -ApiBaseUrl https://app-moe-studentfinance-api-uat.azurewebsites.net `
  -FrontendBaseUrl https://<static-web-app-hostname> `
  -RequireSwagger
```

Script kiểm tra:

- `/health/live`
- `/health/ready`
- Swagger nếu bật.
- Header `X-Correlation-Id`.
- SignalR hub và negotiate auth gate.
- Frontend SPA fallback nếu truyền frontend URL.
