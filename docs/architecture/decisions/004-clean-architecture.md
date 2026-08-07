# ADR 004: Clean Architecture (Four-Layer)

**Date:** August 2026  
**Status:** Accepted and Implemented

---

## Context

The original solution used a single shared `Core` project for domain types, DTOs, interfaces, and service implementations all lived in `ApiService`. This created:
- A monolithic API project with mixed responsibilities (HTTP, business logic, data access)
- No compiler-enforced boundary between layers
- Difficulty identifying where new code should go

## Decision

Adopt a pragmatic Clean Architecture with four layers enforced by project references. Dependencies flow inward only — outer layers depend on inner layers, never the reverse.

## Solution Structure (9 Projects)

```
AspireWebAppTemplate/
├── AspireWebAppTemplate.AppHost/             ← Aspire orchestrator
├── AspireWebAppTemplate.ServiceDefaults/     ← Aspire cross-cutting defaults
├── AspireWebAppTemplate.Domain/              ← Layer 1: Domain vocabulary
├── AspireWebAppTemplate.Application/         ← Layer 2: Use cases & contracts
├── AspireWebAppTemplate.Infrastructure/      ← Layer 3: Framework integrations
├── AspireWebAppTemplate.ApiService/          ← Layer 4: Thin HTTP host
├── AspireWebAppTemplate.Web/                 ← Blazor Server frontend
├── AspireWebAppTemplate.UI/                  ← Shared Razor Class Library
└── AspireWebAppTemplate.Tests/               ← All tests
```

## Dependency Direction

```
ApiService     → Application + Infrastructure + ServiceDefaults
Infrastructure → Application → Domain
Application    → Domain
Web            → Application + UI + ServiceDefaults
UI             → (standalone — MudBlazor components only)
Domain         → nothing (zero dependencies)
Tests          → Application + Infrastructure + Web + ApiService + AppHost
ServiceDefaults → (standalone — Aspire packages only)
AppHost        → ApiService + Web (orchestration references only)
```

The compiler enforces this via `ProjectReference` — if a reference doesn't exist in the .csproj, the dependency is impossible.

## Layer Responsibilities

### Domain (innermost — zero dependencies)
- **Enums/** — All domain enumerations (AuditActionType, ThemePreference, NotificationCategory, etc.)
- **Constants/** — Shared constants (SystemPageDefaults, DateTimeFormatDefaults, ExportDefaults)
- **Attributes/** — Custom validation/metadata attributes (ExportColumnAttribute, OptionalPhoneAttribute)
- **Entities/** — Pure domain entities with no Identity relationship (EmailTemplate)

### Application (depends on Domain only)
- **Abstractions/** — All service interfaces (IAuditLogService, IRoleService, INavigationProvider, ICurrentUserAccessor, etc.)
- **Common/** — Shared models (ApiResult, NavItem, PagedResult)
- **Contracts/** — DTOs grouped by feature (Auth/, Users/, Roles/, AuditLog/, Notifications/, Announcements/, Email/, PagePermissions/, Ai/)
- **Extensions/** — Extension methods (NavigationProviderExtensions, QueryableExtensions)
- **Utilities/** — Pure-logic implementations with no external dependencies (DefaultNavigationProvider, TimeZoneService)

### Infrastructure (depends on Application)
- **Identity/** — ASP.NET Core Identity entities (ApplicationUser, ApplicationRole)
- **Data/** — ApplicationDbContext, Configurations, Entities, Migrations, SeedData
- **Services/** — All business service implementations
- **Clients/** — Typed HttpClients (WebCallbackClient)
- **Handlers/** — Delegating handlers (InternalApiKeyDelegatingHandler)
- **Extensions/** — DI registration (InfrastructureServiceExtensions → `AddInfrastructureServices()`)
- **Options/** — Configuration option classes (LdapSettings)
- **Utilities/** — Helper classes (AuditChangeHelper, CurrentUserAccessor, SecureConnectionString)

### ApiService (thin HTTP host — depends on Application + Infrastructure)
- **Controllers/** — Thin REST controllers extending BaseController
- **Authentication/** — InternalAuthenticationHandler (service-to-service auth)
- **Program.cs** — Composition root (DI, middleware, Identity, EF Core)

## Entity Placement Rule

An entity belongs in **Domain/Entities** only if it has no relationship (inheritance or foreign key) to ASP.NET Core Identity types. An entity belongs in **Infrastructure/Data/Entities** if it references ApplicationUser or ApplicationRole by foreign key. **Infrastructure/Identity** is reserved for types that inherit Identity base classes.

| Location | When to use | Examples |
|----------|-------------|----------|
| `Domain/Entities/` | No Identity relationship whatsoever | EmailTemplate |
| `Infrastructure/Identity/` | Inherits IdentityUser/IdentityRole | ApplicationUser, ApplicationRole |
| `Infrastructure/Data/Entities/` | Has FK to user/role | Announcement, AuditLogEntry, Notification, PagePermission |

## EF Core Migrations

Migrations live in `Infrastructure/Data/Migrations/`. Commands use:
```bash
dotnet ef migrations add MigrationName --project AspireWebAppTemplate.Infrastructure --startup-project AspireWebAppTemplate.ApiService
dotnet ef database update --project AspireWebAppTemplate.Infrastructure --startup-project AspireWebAppTemplate.ApiService
```

`Program.cs` specifies `MigrationsAssembly("AspireWebAppTemplate.Infrastructure")` in the `UseSqlServer` configuration.

## Web Project Access

The Web project references Application (for interfaces, DTOs, contracts, and pure-logic utilities like DefaultNavigationProvider and TimeZoneService) and UI (for shared Blazor components). Web never references Infrastructure or Domain directly.

## Decisions NOT Taken

### No Platform/Business folder split
The design phase considered separating template-owned code (`Platform/`) from business-specific code (`Business/`) within each layer. This was rejected because:
- It adds cognitive overhead on every new file (deciding ownership)
- It deepens folder hierarchies without proportional benefit
- Git branch management handles template-to-project propagation more naturally than folder conventions
- The flat structure is immediately navigable

### No InternalsVisibleTo enforcement
The design phase considered making Domain types `internal` with `InternalsVisibleTo` to prevent Web from accessing Domain directly. This was rejected because:
- It adds friction to everyday development
- The transitive access is harmless in practice (Web using an enum from Domain is fine)
- The project reference structure already communicates intent

### No Enterprise.AspireWebAppPlatform.* rename
The design phase considered giving UI and ServiceDefaults a fixed namespace that never changes on copy-and-rename. This was rejected because:
- The rename adds namespace verbosity
- It only matters in a formal template distribution workflow that doesn't exist yet
- All projects use the same `AspireWebAppTemplate.*` prefix for consistency

### No repository pattern
Services access `ApplicationDbContext` directly. EF Core IS the repository/unit-of-work. No additional abstraction layer.

### No MediatR / domain events
Direct service calls are sufficient at current scale. May revisit if cross-cutting concerns exceed 15+ triggers.

## Consequences

- **Positive:** Clear boundaries, compiler-enforced dependencies, easy to navigate
- **Positive:** ApiService is thin (~200 lines of controllers + Program.cs) — all logic testable through Infrastructure/Services without HTTP
- **Positive:** Domain and Application are framework-free — could theoretically be reused in other hosts
- **Positive:** Web references only Application and UI — fully decoupled from Infrastructure implementation details
- **Negative:** More projects to manage (9 vs 7)
