# Runbook Deploy UAT FE + BE Trên Azure Student

Tài liệu này là điểm bắt đầu chính để deploy UAT cho cả backend và frontend.

## 1. Preflight

Chạy từ backend repo:

```powershell
.\scripts\azure\preflight-uat.ps1 `
  -FrontendRepositoryPath ..\aglie-final-brian-fe
```

Nếu chuẩn bị thao tác với Azure/GitHub thật:

```powershell
.\scripts\azure\preflight-uat.ps1 `
  -FrontendRepositoryPath ..\aglie-final-brian-fe `
  -RequireAzureLogin `
  -RequireGitHubLogin
```

Thêm `-RequireSqlcmd` nếu muốn apply migration từ máy local.

## 2. Provision Azure Resources

Chạy từ backend repo:

```powershell
.\scripts\azure\provision-uat.ps1
```

Script tạo:

- Resource Group
- B1 Linux App Service cho backend
- Static Web App cho frontend
- Storage account/container
- Log Analytics workspace
- Application Insights

Azure SQL để tạo thủ công trong Azure Portal để có thể chọn free/student offer nếu subscription hỗ trợ.

## 3. Cấu Hình Entra Auth

Làm theo checklist:

[UAT_ENTRA_AUTH_CHECKLIST.vi.md](./UAT_ENTRA_AUTH_CHECKLIST.vi.md)

Các giá trị quan trọng:

- SPA redirect URI: `https://<static-web-app-hostname>/admin/login`
- API audience: `api://<uat-api-app-registration-client-id>`
- API scope: `api://<uat-api-app-registration-client-id>/access_as_admin`

## 4. Apply Backend App Service Settings

Copy template thành file local không commit:

```powershell
Copy-Item docs/uat-app-service-settings.template.json .\uat-app-service-settings.local.json
```

Điền hết placeholder trong `uat-app-service-settings.local.json`.

Các URL UAT hiện tại sau provision:

```text
API URL=https://app-moe-studentfinance-api-uat.azurewebsites.net
Frontend URL=https://yellow-sea-0f92b9100.7.azurestaticapps.net
Resource group=moe-studentfinance-uat
Storage account=stmoestudentfinanceuat
Application Insights=appi-moe-studentfinance-uat
Azure SQL server=moedatabase
Azure SQL database=moedb
```

### 4.1. Lấy Các Giá Trị Từ Azure

Application Insights connection string:

```powershell
az monitor app-insights component show `
  --resource-group moe-studentfinance-uat `
  --app appi-moe-studentfinance-uat `
  --query connectionString `
  --output tsv
```

Storage connection string:

```powershell
az storage account show-connection-string `
  --resource-group moe-studentfinance-uat `
  --name stmoestudentfinanceuat `
  --query connectionString `
  --output tsv
```

Static Web App hostname:

```powershell
az staticwebapp show `
  --resource-group moe-studentfinance-uat `
  --name swa-moe-studentfinance-fe-uat `
  --query defaultHostname `
  --output tsv
```

Azure SQL connection string mẫu:

```text
Server=tcp:moedatabase.database.windows.net,1433;Initial Catalog=moedb;Persist Security Info=False;User ID=<sql-admin-user>;Password=<sql-password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

### 4.2. Checklist App Service Settings Cần Điền

| Key | Giá trị UAT / cách lấy |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `UAT` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Lấy bằng command Application Insights ở trên |
| `ConnectionStrings__MoeDatabase` | Azure SQL connection string tới `moedatabase/moedb` |
| `Portals__AdminAllowedOrigins__0` | `https://yellow-sea-0f92b9100.7.azurestaticapps.net` |
| `Portals__EServiceAllowedOrigins__0` | `https://yellow-sea-0f92b9100.7.azurestaticapps.net` |
| `Authentication__AdminEntra__Authority` | `https://login.microsoftonline.com/<uat-tenant-id>/v2.0` |
| `Authentication__AdminEntra__Audience` | `api://<uat-api-app-registration-client-id>` |
| `Authentication__AdminEntra__ClientId` | `<uat-spa-app-registration-client-id>` |
| `Authentication__AdminEntra__Scopes__0` | `api://<uat-api-app-registration-client-id>/access_as_admin` |
| `Authentication__AdminEntra__AllowedTenantId` | `<uat-tenant-id>` |
| `Authentication__AdminEntra__RequireHttpsMetadata` | `true` |
| `Authentication__EServiceSingpass__RedirectUri` | `https://app-moe-studentfinance-api-uat.azurewebsites.net/api/eservice/v1/auth/callback` |
| `Authentication__EServiceSingpass__PortalRedirectUri` | `https://yellow-sea-0f92b9100.7.azurestaticapps.net/portal/login` |
| `AzureBlob__ConnectionString` | Storage connection string ở trên |
| `AzureBlob__ContainerName` | `materials` |
| `FasDocuments__AzureBlobConnectionString` | Storage connection string ở trên |
| `FasDocuments__ContainerName` | `materials` |
| `BackgroundJobs__Enabled` | `true` |
| `UAT__EnableSwagger` | `true` |
| `Stripe__SecretKey` | Secret key test/UAT đã rotate |
| `Stripe__WebhookSecret` | Webhook secret test/UAT đã rotate |
| `Stripe__SuccessUrl` | `https://yellow-sea-0f92b9100.7.azurestaticapps.net/portal/payments/return?checkoutId={CHECKOUT_ID}&result=success&session_id={CHECKOUT_SESSION_ID}` |
| `Stripe__CancelUrl` | `https://yellow-sea-0f92b9100.7.azurestaticapps.net/portal/payments/return?checkoutId={CHECKOUT_ID}&result=cancelled` |
| `MailDelivery__PortalBaseUrl` | `https://yellow-sea-0f92b9100.7.azurestaticapps.net` |
| `MailDelivery__Password` | SMTP password/app password đã rotate |
| `MailDelivery__FallbackPassword` | SMTP fallback password/app password đã rotate |
| `EntraWorkforceDirectory__ClientSecret` | Client secret của app dùng Microsoft Graph |
| `AzureOpenAI__ApiKey` | Azure OpenAI API key nếu chatbot dùng Azure OpenAI |

Nếu chưa có Stripe/Mail/Azure OpenAI thật cho UAT, vẫn phải quyết định giá trị rõ ràng trước khi smoke test các flow liên quan. Không để placeholder vì validator sẽ chặn.

### 4.3. Validate Và Apply

Sau khi điền xong, validate:

```powershell
.\scripts\azure\validate-uat-app-settings.ps1 `
  -Path .\uat-app-service-settings.local.json
```

Apply lên App Service:

```powershell
az webapp config appsettings set `
  --resource-group moe-studentfinance-uat `
  --name app-moe-studentfinance-api-uat `
  --settings "@uat-app-service-settings.local.json"
```

Không commit file `uat-app-service-settings.local.json` vì có secret thật.

## 5. Cấu Hình GitHub Variables Và Secrets

Nếu có GitHub CLI:

```powershell
.\scripts\azure\configure-github-uat.ps1 `
  -BackendRepository "<owner>/<backend-repo>" `
  -FrontendRepository "<owner>/<frontend-repo>" `
  -FrontendBaseUrl "https://<static-web-app-hostname>" `
  -MsalClientId "<uat-entra-client-id>" `
  -MsalTenantId "<uat-tenant-id>"
```

Nếu không có `gh`, set thủ công trong GitHub UI.

Backend secret:

- `AZURE_WEBAPP_PUBLISH_PROFILE`

Frontend secret:

- `AZURE_STATIC_WEB_APPS_API_TOKEN`

Frontend variables:

- `VITE_API_BASE_URL`
- `VITE_MSAL_CLIENT_ID`
- `VITE_MSAL_TENANT_ID`
- `VITE_ENTRA_USER_DOMAIN`
- `VITE_HITPAY_REDIRECT_BASE`
- `VITE_CHATBOT_ENDPOINT`
- `VITE_ENABLE_DEV_CLOCK=false`
- `VITE_DEFAULT_ORGANIZATION_ID=2`

## 6. Push Branch Deploy

Cả hai repo dùng branch:

```text
brian/deploy
```

Thứ tự khuyến nghị:

1. Chạy backend workflow.
2. Download artifact `uat-migration-script`.
3. Apply `migrate-uat.sql` vào Azure SQL.
4. Restart backend App Service.
5. Chạy frontend workflow.
6. Chạy backend smoke workflow.
7. Chạy frontend smoke workflow.

## 7. Apply Database Migration

Sau khi backend workflow sinh artifact `migrate-uat.sql`, có thể apply bằng:

```powershell
.\scripts\azure\apply-uat-migration.ps1 `
  -MigrationScriptPath .\migrate-uat.sql `
  -SqlServerName "<azure-sql-server-name>" `
  -DatabaseName "sqldb-moe-studentfinance-uat" `
  -SqlUser "<sql-admin-user>" `
  -SqlPassword (Read-Host "SQL password" -AsSecureString)
```

Script migration là idempotent, nhưng vẫn nên review trước khi apply.

## 8. Smoke Test

Backend:

```powershell
.\scripts\azure\test-uat.ps1 `
  -ApiBaseUrl https://app-moe-studentfinance-api-uat.azurewebsites.net `
  -FrontendBaseUrl https://<static-web-app-hostname> `
  -RequireSwagger
```

Frontend:

```bash
node scripts/azure/test-uat.mjs https://<static-web-app-hostname>
```

Kiểm tra thủ công:

- Admin login bằng Entra.
- Student portal login redirect đúng về Static Web App.
- SignalR notification kết nối `wss://<api-app>/hubs/notifications`.
- Lỗi có `X-Correlation-Id` và tìm được trong Application Insights.
- Background jobs không tạo duplicate email/record trong UAT.

## 9. Observability

Dùng:

[UAT_APPLICATION_INSIGHTS_QUERIES.vi.md](./UAT_APPLICATION_INSIGHTS_QUERIES.vi.md)

để tạo query/dashboard trong Application Insights.

## 10. Teardown Sau UAT

Khi không dùng UAT nữa, xóa resource group để tránh tốn Azure Student credit:

```powershell
.\scripts\azure\teardown-uat.ps1 `
  -ResourceGroup moe-studentfinance-uat
```
