# MOE Codebase Agent Skill

Use this when modifying the MOE Student Finance codebase.

## Goal

Keep the codebase readable for a small team. Prefer clear module boundaries, explicit names, and boring maintainable code.

## Architecture Rules

1. Treat the solution as a modular monolith.
2. Deploy through `src/Hosts/Moe.StudentFinance.Api`.
3. Put feature code inside its owning module under `src/Modules/<ModuleName>`.
4. Keep controllers thin.
5. Keep use cases in `Application`.
6. Keep entities, constants, invariants, and domain errors in `Domain`.
7. Put contracts in `IGateway`.
8. Put EF Core, SQL, provider SDKs, HTTP clients, and external integrations in `Infrastructure`.
9. Do not add `.Contracts` projects unless explicitly requested.
10. Do not create generic repositories.

## Handler Rules

Handlers should follow this flow:

```text
Validate actor
Load state through repository/gateway
Check rules
Perform domain operation
Call repository/gateway
Return Result<T>
```

Do not inject `MoeDbContext` into handlers. Do not use EF Core in `Application` or `Api`.
Controllers should use `ICommandDispatcher` and `IQueryDispatcher`; do not inject individual handlers into controllers.

## Repository Rules

Repository interfaces go in:

```text
<Module>/IGateway/Repositories
```

Implementations go in:

```text
<Module>/Infrastructure/Repositories
```

Repositories own EF queries and persistence. Application use cases should not call `SaveChangesAsync`.

## Gateway Rules

Use gateways for external systems or cross-module capabilities:

```text
IEntraWorkforceDirectoryClient
ISingpassLoginGateway
IEducationAccountProvisioningGateway
```

External provider models must not leak into application handlers.

## Shared Rules

Use shared projects sparingly:

- `Moe.SharedKernel`: entity primitives, `Result`, `Error`.
- `Moe.Application.Abstractions`: command/query handlers, current user, clock.
- `Moe.Infrastructure.Shared`: middleware, auth setup, logging, response mapping.

Do not move business statuses or repositories into shared projects.

## Naming Rules

Prefer names that explain the business action:

```text
CreateAdminUserCommand
ProvisionStudentSingpassAccountHandler
EducationAccountRepository
UserAccessScope
```

Reject vague names like `Helper`, `Manager`, `Util`, `Data`, `Info`, and broad `Service`.

## Verification

Before finishing:

```powershell
dotnet build
dotnet test --no-build
```

Also scan changed application/API code for accidental EF usage:

```powershell
rg "MoeDbContext|Microsoft.EntityFrameworkCore|\\.Set<|AnyAsync|SingleOrDefaultAsync" src/Modules/*/*/Application src/Modules/*/*/Api
```
