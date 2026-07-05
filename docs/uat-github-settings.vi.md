# GitHub Settings Cho Backend UAT

Set các giá trị này trước khi chạy backend workflow:

```text
.github/workflows/deploy-uat.yml
```

## Repository Variables

| Name | Example |
| --- | --- |
| `AZURE_WEBAPP_NAME` | `app-moe-studentfinance-api-uat` |
| `UAT_API_BASE_URL` | `https://app-moe-studentfinance-api-uat.azurewebsites.net` |
| `UAT_FRONTEND_BASE_URL` | `https://<static-web-app-hostname>` |

## Repository Secret

| Name | Source |
| --- | --- |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | Azure Portal > App Service > Get publish profile |

## App Service Settings

Copy template:

```powershell
Copy-Item docs/uat-app-service-settings.template.json .\uat-app-service-settings.local.json
```

Điền giá trị thật, validate:

```powershell
.\scripts\azure\validate-uat-app-settings.ps1 `
  -Path .\uat-app-service-settings.local.json
```

Apply:

```powershell
az webapp config appsettings set `
  --resource-group moe-studentfinance-uat `
  --name app-moe-studentfinance-api-uat `
  --settings "@uat-app-service-settings.local.json"
```

## Static Web Apps Token

Token này dùng cho frontend repository:

```powershell
az staticwebapp secrets list `
  --name swa-moe-studentfinance-fe-uat `
  --resource-group moe-studentfinance-uat `
  --query properties.apiKey `
  --output tsv
```

## Platform Toggles

```powershell
az webapp config set `
  --resource-group moe-studentfinance-uat `
  --name app-moe-studentfinance-api-uat `
  --always-on true `
  --web-sockets-enabled true `
  --https-only true
```
