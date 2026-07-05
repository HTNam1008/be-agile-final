# UAT Entra Auth Checklist

Use this checklist before the first UAT login test. The frontend starts admin login by calling the backend auth-flow endpoint, so the backend App Service settings are the source of truth for authority, audience, client id, and scopes.

## 1. Create or verify Entra app registrations

Create one SPA app registration for the UAT frontend:

- Platform: Single-page application.
- Redirect URI: `https://<static-web-app-hostname>/admin/login`.
- Logout URL, if configured: `https://<static-web-app-hostname>/admin/login`.
- Supported account type: single tenant for the UAT tenant.

Create one API app registration for the UAT backend:

- Application ID URI: `api://<uat-api-app-registration-client-id>`.
- Exposed scope: `access_as_admin`.
- Admin consent: grant the SPA app permission to call `access_as_admin`.

## 2. Configure backend App Service settings

Fill these keys in `uat-app-service-settings.local.json` and apply them to App Service:

- `Authentication__AdminEntra__Authority=https://login.microsoftonline.com/<uat-tenant-id>/v2.0`
- `Authentication__AdminEntra__Audience=api://<uat-api-app-registration-client-id>`
- `Authentication__AdminEntra__ClientId=<uat-spa-app-registration-client-id>`
- `Authentication__AdminEntra__Scopes__0=api://<uat-api-app-registration-client-id>/access_as_admin`
- `Authentication__AdminEntra__AllowedTenantId=<uat-tenant-id>`
- `Authentication__AdminEntra__RequireHttpsMetadata=true`

Validate before applying:

```powershell
.\scripts\azure\validate-uat-app-settings.ps1 `
  -Path .\uat-app-service-settings.local.json
```

## 3. Configure frontend GitHub variables

The frontend workflow uses repository variables at build time:

- `VITE_API_BASE_URL=https://app-moe-studentfinance-api-uat.azurewebsites.net`
- `VITE_MSAL_CLIENT_ID=<uat-spa-app-registration-client-id>`
- `VITE_MSAL_TENANT_ID=<uat-tenant-id>`
- `VITE_HITPAY_REDIRECT_BASE=https://<static-web-app-hostname>`
- `VITE_CHATBOT_ENDPOINT=https://app-moe-studentfinance-api-uat.azurewebsites.net/api/chatbot`

## 4. Verify the auth flow

After BE and FE are deployed:

1. Open `https://<static-web-app-hostname>/admin/login`.
2. Confirm Entra redirects back to `/admin/login` with an authorization code.
3. Confirm the frontend exchanges the code and the backend creates an admin session cookie.
4. Open Application Insights Logs and search the request by `X-Correlation-Id` if login fails.
5. Confirm SignalR connects after login to `wss://<api-app-name>.azurewebsites.net/hubs/notifications`.

Common failures:

- `AADSTS50011`: the SPA redirect URI does not exactly match `/admin/login`.
- `invalid_scope`: the API app registration did not expose `access_as_admin`, or admin consent was not granted.
- `401` from backend APIs: `Audience`, `AllowedTenantId`, or `Scopes__0` does not match the token.
- SignalR `401` or `403`: auth cookie/token was not established before the hub connection starts.
