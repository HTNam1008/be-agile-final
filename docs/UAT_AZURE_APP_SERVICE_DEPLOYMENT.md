# UAT Azure App Service Deployment

This branch deploys the ASP.NET Core API to Azure App Service for UAT.

For the full FE + BE order of operations, start with `docs/UAT_DEPLOYMENT_RUNBOOK.md`.

## Azure resources

- Resource group: `moe-studentfinance-uat`
- Region: `eastasia` by default because the Azure for Students policy allows it in this subscription.
- App Service Plan: B1 Linux, for example `asp-moe-studentfinance-uat`
- App Service: `app-moe-studentfinance-api-uat`
- Azure SQL Database: `sqldb-moe-studentfinance-uat`
- Storage container: `materials`
- Application Insights: `appi-moe-studentfinance-uat`

Use B1 Linux for UAT because the API self-hosts SignalR and needs more reliable WebSocket support than Free F1.

When UAT is no longer needed, delete the UAT resource group with `scripts/azure/teardown-uat.ps1` to stop Azure Student credit consumption. Stopping only the App Service app does not remove the App Service Plan cost.

You can create the repeatable Azure skeleton with:

```powershell
.\scripts\azure\provision-uat.ps1
```

The script creates the resource group, B1 Linux App Service, Static Web App, Storage account/container, Log Analytics workspace, and Application Insights. Azure SQL is intentionally left as a manual step so you can select the Azure SQL free offer when it is available in your student subscription.

## GitHub configuration

Repository variable:

- `AZURE_WEBAPP_NAME`: App Service name.

Repository secret:

- `AZURE_WEBAPP_PUBLISH_PROFILE`: publish profile downloaded from the App Service.

See `docs/uat-github-settings.md` for a copy-ready checklist.

The workflow `.github/workflows/deploy-uat.yml` builds, publishes the API, generates an idempotent EF migration script, uploads that script as an artifact, and deploys the API package. The full test suite can be enabled manually with the `run_tests` workflow input; it is optional for UAT deploy because the current repository has pre-existing date/test-data failures unrelated to deployment wiring.

After the first database migration is applied, the same workflow can run smoke checks with the manual `run_smoke` input. This uses `UAT_API_BASE_URL` and optional `UAT_FRONTEND_BASE_URL` repository variables.

## App Service settings

Set these in Azure App Service Configuration:

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

A placeholder JSON template is available at `docs/uat-app-service-settings.template.json`.
Before applying a filled copy, validate it with `scripts/azure/validate-uat-app-settings.ps1` so placeholders or localhost URLs do not reach UAT.

Enable these App Service platform settings:

- WebSockets: On
- Always On: On
- HTTPS Only: On

For the first UAT deployment, run the existing hosted background jobs inside the B1 Linux App Service. This keeps the system functional without a separate worker deployment. If the jobs are later moved to Azure Functions Consumption, set `BackgroundJobs__Enabled=false` in App Service to avoid duplicate polling.

## Database migration

For the first UAT deployment, download the `uat-migration-script` workflow artifact and apply `migrate-uat.sql` manually to the Azure SQL database. Keep automatic startup migrations disabled.

If `sqlcmd` is installed, use `scripts/azure/apply-uat-migration.ps1` to apply the downloaded script with a confirmation prompt.

## Observability

Application Insights is enabled only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is present. The existing correlation middleware keeps `X-Correlation-Id` in responses and log scopes, so UAT testers can report a trace id and operators can search for it in Application Insights.

Set an Application Insights daily cap, such as 100-300 MB/day, to protect Azure Student credit.

See `docs/UAT_APPLICATION_INSIGHTS_QUERIES.md` for copy-ready Logs queries and suggested Workbook tiles.

## Smoke test

After deployment, run:

```powershell
.\scripts\azure\test-uat.ps1 `
  -ApiBaseUrl https://app-moe-studentfinance-api-uat.azurewebsites.net `
  -FrontendBaseUrl https://<static-web-app-hostname> `
  -RequireSwagger
```

The script verifies `/health/live`, `/health/ready`, optional Swagger, API `X-Correlation-Id` headers, the SignalR hub unauthenticated surface, SignalR negotiate auth gating, and Static Web Apps SPA fallback for `/portal/login` when a frontend URL is supplied.

Manual follow-up:

1. Open the frontend and verify CORS succeeds.
2. Sign in as an admin and student UAT account.
3. Verify SignalR connects to `/hubs/notifications`.
4. Trigger a test exception or failed request and confirm it appears in Application Insights.

## Secret hygiene

Secrets that have appeared in repository config should be treated as compromised. Rotate them before public UAT and keep real values only in Azure App Service Configuration, GitHub Secrets, or Key Vault.

For local development, keep real values in user-secrets or environment variables instead of `appsettings*.json`. The tracked JSON files should contain only safe defaults or placeholders.
