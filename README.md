# MOE Student Finance Backend

Backend services for the MOE Student Finance platform. The solution is an
ASP.NET Core .NET 10 modular monolith that supports school administration,
student course enrollment, education accounts, financial assistance, billing,
and online payments.

## Capabilities

- Admin authentication with Microsoft Entra ID and MFA
- Student authentication with Singpass and MockPass for local/UAT use
- Student and organization access management
- Course fees, payment plans, enrollment, billing, and installments
- Financial Assistance Scheme (FAS) configuration and voucher redemption
- Education Account provisioning, top-ups, lifecycle, and interest processing
- Education Account, Stripe, and combined statement payments
- Enrollment cancellation and refunds
- Email delivery, in-app notifications, and SignalR updates
- AI-assisted finance support

## Architecture

The repository uses a modular monolith: business modules are independently
structured projects but are composed into one API deployment.

```text
MOE.StudentFinance.sln
src/
  Hosts/
    Moe.StudentFinance.Api       Main ASP.NET Core API
    Moe.StudentFinance.Worker    Optional background worker
  Modules/
    IdentityPlatform
    Mfa
    CourseBilling
    FasPayment
    EducationAccountTopUp
    Notifications
    MailDelivery
    AiCopilot
  Shared/
    Moe.SharedKernel
    Moe.Application.Abstractions
    Moe.Infrastructure.Shared
  Database/
    Moe.StudentFinance.Persistence
    Moe.StudentFinance.Migrations
tests/                           Unit, integration, architecture, and E2E tests
docs/                            Architecture, business flows, and runbooks
scripts/                         Database and deployment utilities
```

Each business module follows the same dependency direction:

```text
Api -> Application -> Domain
                \-> IGateway <- Infrastructure
```

- `Api` adapts HTTP requests and responses.
- `Application` contains commands, queries, handlers, and validation.
- `Domain` owns entities and business rules.
- `IGateway` defines persistence and integration contracts.
- `Infrastructure` implements gateways with EF Core and external providers.

Controllers and application handlers must not access `MoeDbContext` directly.
Expected business failures are returned with `Result<T>` rather than exceptions.

## API Surfaces

| Route prefix | Audience | Authentication |
|---|---|---|
| `/api/admin/v1` | School and platform administrators | Microsoft Entra ID |
| `/api/eservice/v1` | Students | Singpass/MockPass session |
| `/api/public/v1` | Public discovery endpoints | Endpoint-specific |

Swagger is available at `/swagger` when enabled for the active environment.

## Requirements

- .NET SDK 10.0.100 or a compatible .NET 10 SDK
- SQL Server
- Optional local MockPass service for student login
- External provider configuration only for features being exercised

## Local Setup

Restore and build the solution:

```powershell
dotnet restore MOE.StudentFinance.sln
dotnet build MOE.StudentFinance.sln
```

Provide a local database connection without modifying tracked settings:

```powershell
$env:ConnectionStrings__MoeDatabase = "Server=localhost,1433;Database=MOEStudentFinance;User Id=sa;Password=<your-password>;TrustServerCertificate=True"
```

Apply EF Core migrations:

```powershell
dotnet ef database update `
  --project src/Database/Moe.StudentFinance.Migrations `
  --startup-project src/Hosts/Moe.StudentFinance.Api
```

Run the API:

```powershell
dotnet run --project src/Hosts/Moe.StudentFinance.Api
```

The launch profile listens on `https://localhost:7000` and
`http://localhost:7001`.

## Configuration and Secrets

ASP.NET Core configuration supports environment variables using double
underscores for nested keys. Examples:

```text
ConnectionStrings__MoeDatabase
Stripe__SecretKey
Stripe__WebhookSecret
AzureOpenAI__ApiKey
AzureBlob__ConnectionString
Redis__ConnectionString
MailDelivery__Password
MailDelivery__FallbackPassword
EntraWorkforceDirectory__ClientSecret
```

Use environment variables, .NET User Secrets, GitHub environment secrets, or
Azure App Service settings. Values such as Entra client IDs, tenant IDs,
authorities, scopes, and public URLs are identifiers rather than credentials.

Never commit provider keys, connection strings containing passwords, SMTP
passwords, private keys, signing keys, or publish profiles.

## Tests

Run the complete test suite:

```powershell
dotnet test MOE.StudentFinance.sln
```

For a faster API compilation check:

```powershell
dotnet build src/Hosts/Moe.StudentFinance.Api/Moe.StudentFinance.Api.csproj --no-restore
```

## Local Seed Data

After applying migrations, seed the canonical local dataset:

```powershell
.\scripts\seed-local-database.ps1
```

For a custom SQL Server instance:

```powershell
.\scripts\seed-local-database.ps1 `
  -Server "localhost\SQLEXPRESS" `
  -Database "MOEStudentFinance"
```

The seed is idempotent and intended for development and testing only.

## Repository Hygiene

Before publishing or deploying, run the repository secret check:

```powershell
.\scripts\azure\check-uat-repo-hygiene.ps1 `
  -FrontendRepositoryPath "..\aglie-final-brian-fe"
```

Also run the hosting platform's dependency and secret scanners. If a real secret
has ever been committed, remove it from current configuration, rotate it at the
provider, and assess whether Git history must be rewritten.

## Deployment

Database migrations must be generated and reviewed as SQL for UAT/production;
the API must not automatically migrate the database at startup. Deployment
guides are available in:

- [`docs/UAT_AZURE_APP_SERVICE_DEPLOYMENT.md`](docs/UAT_AZURE_APP_SERVICE_DEPLOYMENT.md)
- [`docs/UAT_DEPLOYMENT_RUNBOOK.md`](docs/UAT_DEPLOYMENT_RUNBOOK.md)
- [`docs/uat-github-settings.md`](docs/uat-github-settings.md)

Read [`docs/CODE_CONVENTIONS.md`](docs/CODE_CONVENTIONS.md) before adding a new
module or use case.
