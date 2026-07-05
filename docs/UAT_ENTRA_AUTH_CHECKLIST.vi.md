# Checklist Entra Auth Cho UAT

Frontend admin login gọi backend auth-flow endpoint, nên backend App Service settings là nguồn cấu hình chính cho authority, audience, client id và scopes.

## 1. SPA App Registration Cho Frontend

Tạo hoặc kiểm tra Entra app registration cho frontend:

- Platform: Single-page application.
- Redirect URI: `https://<static-web-app-hostname>/admin/login`
- Logout URL nếu có: `https://<static-web-app-hostname>/admin/login`
- Supported account type: single tenant trong UAT tenant.

## 2. API App Registration Cho Backend

Tạo hoặc kiểm tra Entra app registration cho backend:

- Application ID URI: `api://<uat-api-app-registration-client-id>`
- Expose scope: `access_as_admin`
- Grant admin consent cho SPA app được gọi scope `access_as_admin`.

## 3. Backend App Service Settings

Điền các key này trong `uat-app-service-settings.local.json`:

- `Authentication__AdminEntra__Authority=https://login.microsoftonline.com/<uat-tenant-id>/v2.0`
- `Authentication__AdminEntra__Audience=api://<uat-api-app-registration-client-id>`
- `Authentication__AdminEntra__ClientId=<uat-spa-app-registration-client-id>`
- `Authentication__AdminEntra__Scopes__0=api://<uat-api-app-registration-client-id>/access_as_admin`
- `Authentication__AdminEntra__AllowedTenantId=<uat-tenant-id>`
- `Authentication__AdminEntra__RequireHttpsMetadata=true`

Validate:

```powershell
.\scripts\azure\validate-uat-app-settings.ps1 `
  -Path .\uat-app-service-settings.local.json
```

## 4. Frontend GitHub Variables

Set trong frontend repository:

- `VITE_API_BASE_URL=https://app-moe-studentfinance-api-uat.azurewebsites.net`
- `VITE_MSAL_CLIENT_ID=<uat-spa-app-registration-client-id>`
- `VITE_MSAL_TENANT_ID=<uat-tenant-id>`
- `VITE_HITPAY_REDIRECT_BASE=https://<static-web-app-hostname>`
- `VITE_CHATBOT_ENDPOINT=https://app-moe-studentfinance-api-uat.azurewebsites.net/api/chatbot`

## 5. Verify Login

Sau khi deploy:

1. Mở `https://<static-web-app-hostname>/admin/login`.
2. Entra phải redirect về `/admin/login` kèm authorization code.
3. Frontend exchange code thành token.
4. Backend tạo admin session cookie.
5. SignalR connect được sau login: `wss://<api-app-name>.azurewebsites.net/hubs/notifications`.

## Lỗi Hay Gặp

- `AADSTS50011`: Redirect URI trong Entra không khớp chính xác `/admin/login`.
- `invalid_scope`: API chưa expose `access_as_admin` hoặc chưa grant admin consent.
- Backend trả `401`: `Audience`, `AllowedTenantId` hoặc scope không khớp token.
- SignalR `401/403`: session/token chưa được establish trước khi hub connect.
