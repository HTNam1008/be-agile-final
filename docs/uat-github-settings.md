# UAT GitHub Settings

Set these values before running `.github/workflows/deploy-uat.yml`.

For an end-to-end FE + BE sequence, use `docs/UAT_DEPLOYMENT_RUNBOOK.md`.

## Repository variables

| Name | Example |
| --- | --- |
| `AZURE_WEBAPP_NAME` | `app-moe-studentfinance-api-uat` |
| `UAT_API_BASE_URL` | `https://app-moe-studentfinance-api-uat.azurewebsites.net` |
| `UAT_FRONTEND_BASE_URL` | `https://<static-web-app-hostname>` |

## Repository secrets

| Name | Source |
| --- | --- |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | Azure Portal > App Service > Get publish profile |

## Applying App Service settings

Copy `docs/uat-app-service-settings.template.json` to `uat-app-service-settings.local.json`, replace placeholders, then validate and apply the values with:

Do not commit `uat-app-service-settings.local.json`; it contains real UAT secrets and is ignored by Git.

```powershell
.\scripts\azure\validate-uat-app-settings.ps1 `
  -Path .\uat-app-service-settings.local.json

az webapp config appsettings set `
  --resource-group moe-studentfinance-uat `
  --name app-moe-studentfinance-api-uat `
  --settings "@uat-app-service-settings.local.json"
```

For connection strings, Azure App Service accepts them as app settings with `__` keys because ASP.NET Core maps those keys into configuration sections.

After Static Web Apps is created, get its deployment token for the frontend repository with:

```powershell
az staticwebapp secrets list `
  --name swa-moe-studentfinance-fe-uat `
  --resource-group moe-studentfinance-uat `
  --query properties.apiKey `
  --output tsv
```

## Required platform toggles

```powershell
az webapp config set `
  --resource-group moe-studentfinance-uat `
  --name app-moe-studentfinance-api-uat `
  --always-on true `
  --web-sockets-enabled true `
  --https-only true
```
