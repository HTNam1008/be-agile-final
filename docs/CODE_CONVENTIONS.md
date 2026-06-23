# MOE Code Conventions

Short version: this solution is a modular monolith. Deploy one API host, but keep code separated by business module so teams can work without stepping on each other.

## Project Shape

```text
src/Hosts/Moe.StudentFinance.Api       HTTP entrypoint only
src/Modules/<ModuleName>               Business module
src/Shared/Moe.SharedKernel            Entity, AggregateRoot, Result, Error
src/Shared/Moe.Application.Abstractions Cross-cutting application contracts
src/Shared/Moe.Infrastructure.Shared    Middleware, auth, logging, CORS, API response
src/Database                           DbContext and migrations
```

## Module Shape

Each module should follow this layout:

```text
Api              Controllers/endpoints only
Application      Use cases, commands, queries, validators
Domain           Entities, domain rules, domain errors, constants
IGateway         Interfaces used by Application or other modules
Infrastructure   EF repositories, external clients, provider integrations
```

## Dependency Rule

```text
Api -> Application
Application -> Domain + IGateway + Shared abstractions
Infrastructure -> Domain + IGateway + EF/provider SDKs
Domain -> SharedKernel only
```

Application code must not reference EF Core, `MoeDbContext`, provider SDKs, SQL, HTTP clients, or secrets.

## Use Cases

Use one handler per use case.

Handler order:

```text
Validate actor
Load state through repository/gateway
Check business rules
Create/change domain objects
Call repository/gateway to persist or integrate
Return Result<T>
```

Handlers should stay readable. If a handler needs too many dependencies or has many private helper methods, extract a focused application service.

## Commands And Queries

- `ICommand<TResponse>`: changes state.
- `IQuery<TResponse>`: reads state.
- `ICommandHandler` / `IQueryHandler`: application use case boundary.
- Controllers call `ICommandDispatcher` / `IQueryDispatcher`; controllers do not inject concrete handlers.
- Controllers do not contain business logic.

## Repositories And Gateways

- Repositories live in `Infrastructure/Repositories`.
- Repository interfaces live in `IGateway/Repositories`.
- External service interfaces live in `IGateway/<Area>`.
- EF queries and `SaveChangesAsync` stay inside infrastructure.
- Do not create a generic repository.
- Do not write across another module's tables directly. Use that module's gateway contract.

## Domain Rules

- Domain contains entities, invariants, status constants, and domain errors.
- Avoid string literals in use cases. Put stable codes in domain constants.
- Expected business failures return `Result` / `Result<T>`.
- Unexpected infrastructure failures may throw and are mapped by middleware.

## Middleware

Use middleware only for cross-cutting HTTP concerns:

- exception mapping
- API response/error shape
- request logging
- trace/correlation id
- performance timing
- security headers

Do not put business use-case logic in middleware.

## Naming

Use explicit names:

```text
CreateAdminUserCommand
ProvisionStudentSingpassAccountHandler
IdentityProvisioningRequest
EducationAccountProvisioningGateway
RequestedByUserAccountId
CompletedAtUtc
```

Avoid vague names:

```text
Helper
Manager
Util
Data
Info
Processor
Service  // unless it has a clear domain/application responsibility
```

## Time And Security

- Use `IClock` instead of `DateTime.UtcNow` in application/domain orchestration.
- Store and compare UTC timestamps; suffix names with `Utc`.
- Do not log tokens, secrets, NRIC/raw identifiers, or full auth payloads.
- Keep secrets out of `appsettings.json`; use environment variables or secret stores.

## Review Checklist

- Does the controller only adapt HTTP?
- Does the handler read like one business use case?
- Are EF/provider details only in infrastructure?
- Are business codes named constants?
- Are expected failures returned as `Result<T>`?
- Is the change module-owned and migration-safe?
- Did `dotnet build` and tests pass?
