# UAT Deployment Runbook

Use this runbook for the first Azure Student UAT deployment of the backend and frontend.

## 1. Preflight

From the backend repository, verify local tools, branch names, and required deployment files:
The preflight also scans UAT deploy files for high-confidence secret patterns.

```powershell
.\scripts\azure\preflight-uat.ps1 `
  -FrontendRepositoryPath ..\aglie-final-brian-fe
```

Before running commands that touch Azure or GitHub, run the stricter check:

```powershell
.\scripts\azure\preflight-uat.ps1 `
  -FrontendRepositoryPath ..\aglie-final-brian-fe `
  -RequireAzureLogin `
  -RequireGitHubLogin
```

Add `-RequireSqlcmd` if you plan to apply Azure SQL migration from this machine.

## 2. Provision Azure resources

From the backend repository:

```powershell
.\scripts\azure\provision-uat.ps1
```

The script creates the resource group, B1 Linux App Service, Static Web App, Storage account/container, Log Analytics workspace, and Application Insights.

Create Azure SQL manually after this step so you can select the Azure SQL free offer if it is available in the student subscription.

## 3. Configure Entra auth

Before applying App Service settings, complete `docs/UAT_ENTRA_AUTH_CHECKLIST.md`.

The most important values are:

- SPA redirect URI: `https://<static-web-app-hostname>/admin/login`
- API audience: `api://<uat-api-app-registration-client-id>`
- API scope: `api://<uat-api-app-registration-client-id>/access_as_admin`

## 4. Apply backend App Service settings

Copy `docs/uat-app-service-settings.template.json` to a local untracked file, replace every placeholder, validate it, then apply it:

```powershell
Copy-Item docs/uat-app-service-settings.template.json .\uat-app-service-settings.local.json

.\scripts\azure\validate-uat-app-settings.ps1 `
  -Path .\uat-app-service-settings.local.json
```

`uat-app-service-settings.local.json` is ignored by Git and should stay local because it contains real UAT secrets.

```powershell
az webapp config appsettings set `
  --resource-group moe-studentfinance-uat `
  --name app-moe-studentfinance-api-uat `
  --settings "@uat-app-service-settings.local.json"
```

Keep real secrets in Azure App Service Configuration, GitHub Secrets, or Key Vault. Do not put real secrets back into `appsettings*.json`.

## 5. Configure GitHub variables and secrets

Set non-secret repository variables with GitHub CLI:

```powershell
.\scripts\azure\configure-github-uat.ps1 `
  -BackendRepository "<owner>/<backend-repo>" `
  -FrontendRepository "<owner>/<frontend-repo>" `
  -FrontendBaseUrl "https://<static-web-app-hostname>" `
  -MsalClientId "<uat-entra-client-id>" `
  -MsalTenantId "<uat-tenant-id>"
```

Then set secrets manually:

- Backend: `AZURE_WEBAPP_PUBLISH_PROFILE`
- Frontend: `AZURE_STATIC_WEB_APPS_API_TOKEN`

In the frontend repository, validate `.env.production` before using it for any local UAT build:

```powershell
node scripts/azure/validate-uat-env.mjs .env.production
```

Get the Static Web Apps token with:

```powershell
az staticwebapp secrets list `
  --name swa-moe-studentfinance-fe-uat `
  --resource-group moe-studentfinance-uat `
  --query properties.apiKey `
  --output tsv
```

## 6. Push deploy branches

Push `brian/deploy` in both repositories. The workflows deploy on push and can also be run manually from GitHub Actions.
The FE and BE deploy workflows use GitHub Actions concurrency so only one UAT deploy per repo runs at a time.

Recommended first run:

1. Run backend workflow and wait for the `uat-migration-script` artifact.
2. Apply `migrate-uat.sql` to Azure SQL.
3. Restart the backend App Service.
4. Run frontend workflow.
5. Re-run the backend workflow manually with `run_smoke=true`, or run the separate `uat-smoke-test` workflow.
6. Run the frontend repository `frontend-uat-smoke-test` workflow to verify Static Web Apps SPA routes.

To apply the migration script with `sqlcmd`:

```powershell
.\scripts\azure\apply-uat-migration.ps1 `
  -MigrationScriptPath .\migrate-uat.sql `
  -SqlServerName "<azure-sql-server-name>" `
  -DatabaseName "sqldb-moe-studentfinance-uat" `
  -SqlUser "<sql-admin-user>" `
  -SqlPassword (Read-Host "SQL password" -AsSecureString)
```

The generated script is idempotent, but still review it before applying it to UAT.

## 7. Smoke test UAT

You can run the backend repository workflow `uat-smoke-test` from GitHub Actions. It uses these backend repository variables by default:

- `UAT_API_BASE_URL`
- `UAT_FRONTEND_BASE_URL`

For later redeploys, the backend `backend-uat-deploy` workflow also has a manual `run_smoke` option. Keep it off for the very first deploy until Azure SQL migration has been applied.

Or run the same checks locally:

```powershell
.\scripts\azure\test-uat.ps1 `
  -ApiBaseUrl https://app-moe-studentfinance-api-uat.azurewebsites.net `
  -FrontendBaseUrl https://<static-web-app-hostname> `
  -RequireSwagger
```

The frontend repository also has a `frontend-uat-smoke-test` workflow. It verifies that `/`, `/admin/login`, and `/portal/login` return the Vite SPA shell from Static Web Apps.

Confirm these manually:

- Admin login works with UAT Entra settings.
- Student portal login redirects back to the Static Web App URL.
- SignalR notification connection uses `wss://<api-app>/hubs/notifications`.
- A failed request appears in Application Insights and can be searched with its `X-Correlation-Id`.
- Background polling does not create duplicate records or duplicate emails during UAT.

## 8. Observe UAT

Use `docs/UAT_APPLICATION_INSIGHTS_QUERIES.md` to create Application Insights Logs queries or Workbook tiles for failed requests, exceptions, dependency failures, background job errors, and SignalR notification clues.

## 9. Teardown UAT

When UAT is finished, delete the resource group to stop App Service Plan, Storage, SQL, Log Analytics, and Application Insights cost:

```powershell
.\scripts\azure\teardown-uat.ps1 `
  -ResourceGroup moe-studentfinance-uat
```

The script asks you to type the resource group name before deletion. Use `-NoWait` if you want Azure to delete in the background.
