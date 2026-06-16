# MOE Student Finance

MOE Student Finance is a .NET 10 modular monolith. The repository is split into small projects for readability and team ownership, but deployment still goes through one main API host.

## Codebase Overview

```text
MOE.StudentFinance.sln
src/
  Hosts/
    Moe.StudentFinance.Api          Main ASP.NET Core API host
    Moe.StudentFinance.Worker       Background worker host
  Modules/
    IdentityPlatform/                Admin identity, Entra ID, Singpass/MockPass, user access
    EducationAccountTopUp/                 Education accounts and account provisioning
    CourseBilling/                Course, billing, subsidy and FAS extension area
    FasPayment/                Payment and digital integration extension area
  Shared/
    Moe.SharedKernel                Entity, AggregateRoot, Result, Error
    Moe.Application.Abstractions    ICommand, IQuery, dispatchers, handlers, IClock, current user
    Moe.Infrastructure.Shared       Middleware, auth setup, logging, API response mapping
  Database/
    Moe.StudentFinance.Persistence  DbContext
    Moe.StudentFinance.Migrations   EF Core migrations
tests/
  Moe.EducationAccountTopUp.UnitTests
frontend/
  MOE login/admin test client
docs/
  Architecture and code convention notes
```

## Runtime Shape

- One API host: `src/Hosts/Moe.StudentFinance.Api`.
- Two API surfaces:
  - `/api/admin/v1` for admin users using Microsoft Entra ID.
  - `/api/eservice/v1` for students using Singpass/MockPass.
- One SQL Server database.
- One optional worker host.
- Multiple module projects compiled into the API output.

## Module Structure

Each business module follows the same shape:

```text
Api              Controllers only
Application      Commands, queries, handlers, FluentValidation
Domain           Entities, domain rules, errors, constants
IGateway         Repository and integration contracts
Infrastructure   EF repositories, persistence mapping, external clients
```

The key rule is simple:

```text
Application should express the use case.
Infrastructure should know EF Core and external systems.
Domain should own business rules.
Api should only adapt HTTP.
```

## Main Modules

| Module | Main Responsibility |
|---|---|
| `IdentityPlatform` | Admin authentication, Entra user provisioning, Singpass/MockPass login, user accounts, access scopes |
| `EducationAccountTopUp` | Education account creation, student account-holder provisioning, account lifecycle |
| `CourseBilling` | Future academic finance workflows |
| `FasPayment` | Future payment and digital integration workflows |

## Authentication Flow

- Admin users authenticate through Microsoft Entra ID.
- Students authenticate through Singpass. Local development uses MockPass.
- Admin accounts cannot self-register; an existing admin creates another admin.
- Student Singpass accounts must be provisioned locally before login succeeds.
- If a student is an account holder, the system creates an education account with initial balance `0`.

## Code Rules

- Do not put EF Core in `Application` or `Api`.
- Do not inject `MoeDbContext` into handlers/controllers.
- Controllers dispatch commands/queries through `ICommandDispatcher` and `IQueryDispatcher`.
- Use repositories/gateways through `IGateway` contracts.
- Keep business constants in `Domain`.
- Use `Result<T>` for expected business failures.
- Use middleware only for cross-cutting HTTP concerns.
- Keep secrets out of source-controlled settings.

Read [docs/CODE_CONVENTIONS.md](docs/CODE_CONVENTIONS.md) before adding features. Future coding agents should follow [docs/AGENT_CODEBASE_SKILL.md](docs/AGENT_CODEBASE_SKILL.md).

## Local commands

```powershell
dotnet restore MOE.StudentFinance.sln
dotnet build MOE.StudentFinance.sln
dotnet test MOE.StudentFinance.sln

dotnet ef migrations add InitialCreate `
  --project src/Database/Moe.StudentFinance.Migrations `
  --startup-project src/Hosts/Moe.StudentFinance.Api
```

The API must not automatically apply migrations in UAT. Generate and review SQL scripts in the deployment pipeline.
