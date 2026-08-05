# AspireWebAppTemplate — Architecture Decisions

**Purpose:** Document the agreed architectural decisions for AspireWebAppTemplate's long-term structure. This document serves as the authoritative reference for the migration from the current architecture to Clean Architecture.

**Date:** August 3, 2026

---

## 1. Chosen Architecture: Clean Architecture (Pragmatic)

After evaluating the current architecture, full Clean Architecture, hybrid approaches, vertical slices, and modular monolith patterns, the decision is to adopt **standard Clean Architecture** with pragmatic adjustments suited to the project's nature as a reusable enterprise template.

### Key Principles

- **Dependency inversion via project references** — inner layers define interfaces, outer layers implement them
- **Compiler-enforced boundaries** — if a `ProjectReference` doesn't exist, the dependency is impossible
- **No unnecessary abstractions** — no repository pattern over EF Core, no MediatR, no domain events unless complexity demands them
- **Template-first simplicity** — every architectural choice must be easy to understand for developers who copy and extend the template

---

## 2. Solution Structure

```
AspireWebAppTemplate/
├── AspireWebAppTemplate.AppHost/              ← Aspire orchestrator (dev entry point)
├── AspireWebAppTemplate.ServiceDefaults/      ← Aspire cross-cutting defaults (telemetry, health, resilience)
├── AspireWebAppTemplate.Domain/               ← Domain vocabulary (enums, value objects, constants)
├── AspireWebAppTemplate.Application/          ← Use cases (service interfaces, DTOs/Contracts, shared models)
├── AspireWebAppTemplate.Infrastructure/       ← Framework integrations (EF Core, Identity, SMTP, LDAP, AWS)
├── AspireWebAppTemplate.ApiService/           ← Thin HTTP host (controllers + composition root)
├── AspireWebAppTemplate.Web/                  ← Blazor Server frontend
├── AspireWebAppTemplate.UI/                   ← Shared Razor Class Library (MudBlazor components)
└── AspireWebAppTemplate.Tests/                ← All tests (property-based, unit, integration)
```

---

## 3. Dependency Direction

```
ApiService     → Application + Infrastructure + ServiceDefaults
Infrastructure → Application → Domain
Application    → Domain
Web            → Application + UI + ServiceDefaults
UI             → (standalone — MudBlazor components only)
Domain         → nothing
Tests          → Application + Infrastructure + Web + ApiService + AppHost
ServiceDefaults → (standalone — Aspire packages only)
AppHost        → ApiService + Web (orchestration references only)
```

### Enforcement

Boundaries are enforced by **project references only**. No `InternalsVisibleTo` hacks, no internal visibility tricks. If a project doesn't have a `<ProjectReference>` to another, it cannot use its types. The compiler does the enforcement.

---

## 4. Project Responsibilities

### Domain (innermost — zero dependencies)

The most stable project. Changes only when fundamental business vocabulary or identity-free entities change.

**Contains:**
- Domain entities that have NO relationship to Identity types (e.g., `EmailTemplate` — no FK to user/role)
- Domain enums (`NotificationCategory`, `AuditActionType`, `ThemePreference`, `EmailType`, etc.)
- Value objects (future use — e.g., `EmailAddress`, `PhoneNumber` if validation logic is needed)
- Constants and defaults (`SystemPageDefaults`, `DateTimeFormatDefaults`, `ExportDefaults`)
- Custom attributes (`ExportColumnAttribute`, `OptionalPhoneAttribute`)

**Does NOT contain:**
- Entities that reference `ApplicationUser` or `ApplicationRole` (those go to Infrastructure)
- DTOs (these are API contracts → Application)
- Service interfaces (these define use cases → Application)
- Implementations of any kind
- Any NuGet package references

**Entity placement rule:** An entity belongs here ONLY if it has zero foreign keys or navigation properties to Identity types. If it references a user or role in any way, it belongs in Infrastructure.

**Naming:** `AspireWebAppTemplate.Domain`

### Application (use case layer — depends on Domain only)

Defines what the system can do and the shapes it communicates with. This is the contract surface shared between the API backend and the Web frontend.

**Contains:**
- Service interfaces (`IUserService`, `INotificationService`, `IAuditLogService`, `IAuthService`, `IRoleService`, `IEmailService`, `IEmailTemplateService`, `IPagePermissionService`, `INavigationService`, `IExcelExportService`, `IAiService`, `ILdapAuthService`, `ILoginService`, `IRegisterService`)
- `ICurrentUserAccessor` interface
- DTOs/Contracts grouped by feature (`Contracts/Users/`, `Contracts/Notifications/`, `Contracts/Auth/`, etc.)
- Shared result types (`PagedResult<T>`, `ApiResult<T>`)
- Navigation models (`NavItem`)
- Shared application abstractions (`INavigationProvider`, `ITimeZoneService`)
- Extension methods that operate on Application/Domain types only (`QueryableExtensions`, `NavigationProviderExtensions`)

**Does NOT contain:**
- Any NuGet package reference beyond the .NET BCL
- Any implementation (service classes, data access, HTTP clients)
- EF Core types, Identity types, or any framework-specific code

**Naming:** `AspireWebAppTemplate.Application`

### Infrastructure (framework layer — depends on Application, transitively Domain)

How the system does what Application defines. All framework-specific code lives here.

**Contains:**
- `ApplicationDbContext` (extends `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`)
- EF Core Entities (`Notification`, `Announcement`, `AuditLogEntry`, `PagePermission`, `EmailTemplate`, `NotificationPreference`, `AnnouncementDismissal`)
- Identity entities (`ApplicationUser`, `ApplicationRole`)
- EF Core Configurations (`IEntityTypeConfiguration<T>` per entity)
- EF Core Migrations
- Seed data (`SeedData` partial class files)
- Service implementations (all `I*Service` implementations from Application)
- `CurrentUserAccessor` implementation
- `AuditChangeHelper` and EF-adjacent utilities
- LDAP/Active Directory integration services
- Email sending (SMTP) implementation
- AI integration (AWS Bedrock) implementation
- Excel export (EPPlus) implementation
- Typed HTTP clients for outbound calls (`WebCallbackClient`)
- Delegating handlers (`InternalApiKeyDelegatingHandler`)
- DI registration extension (`AddInfrastructureServices(this IServiceCollection)`)

**NuGet dependencies:** EF Core, Identity, SQL Server, LDAP, AWS SDK, EPPlus, HtmlSanitizer

**Naming:** `AspireWebAppTemplate.Infrastructure`

### ApiService (HTTP host — depends on Application + Infrastructure)

The composition root for the API. Thin HTTP layer that wires everything together.

**Contains:**
- Controllers (thin — HTTP concerns only, delegate to service interfaces)
- `BaseController` (provides `CurrentUserId`, `CurrentUserName`, `ClientIpAddress`)
- `Program.cs` (composition root: calls `AddInfrastructureServices()`, configures middleware)
- Authentication handlers (`InternalAuthenticationHandler`)
- Health check endpoints
- OpenAPI configuration

**Does NOT contain:**
- Business logic of any kind
- Direct DbContext usage
- Entity-to-DTO mapping logic
- Conditional business rules

**NuGet dependencies:** ASP.NET Core (via SDK), ServiceDefaults

**Naming:** `AspireWebAppTemplate.ApiService`

### Web (Blazor frontend — depends on Application + UI + ServiceDefaults)

The user-facing Blazor Server application.

**Contains:**
- Blazor pages and components (Layout, Pages, Shared)
- Typed HTTP clients for ApiService communication (`ApiUserService`, `ApiNotificationService`, etc.)
- Per-circuit contexts (`NotificationContext`, `PagePermissionContext`, `ThemeContext`, etc.)
- SignalR hubs (`NotificationHub`)
- Authorization handlers (`PagePermissionHandler`)
- Internal endpoints (`NotificationCallbackEndpoint`)
- Authentication handler for internal callbacks (`InternalApiKeyAuthenticationHandler`)
- Delegating handlers (`UserIdentityDelegatingHandler`)
- DI registration extensions (`AddApiClients()`, `AddApplicationServices()`)
- Static assets (wwwroot)

**References Application for:** DTOs (to serialize/deserialize API responses), shared enums (transitively via Application → Domain), `ApiResult<T>`, `PagedResult<T>`, navigation models

**Does NOT reference:** Infrastructure (no EF Core, no direct DB access)

**Naming:** `AspireWebAppTemplate.Web`

### UI (Razor Class Library — standalone)

Reusable Blazor components shared across layouts and pages.

**Contains:**
- Shared components (`PageContent`, `LoadingOverlay`, `PageHeader`, `StatusAlert`, `PillToggle`, `ConfirmationDialog`)
- DataGrid utilities (`DataGridUtils<T>`, `QueryableDataGridUtils<T>`)
- Filter components (`BoolFilterSelect`, `EnumFilterSelect`, `StringFilterSelect`)
- Theme definitions (`DefaultTheme`, `JabilTheme`)

**NuGet dependencies:** MudBlazor, Microsoft.AspNetCore.Components.Web

**Naming:** `AspireWebAppTemplate.UI`

### ServiceDefaults (Aspire defaults — standalone)

Cross-cutting infrastructure concerns provided by .NET Aspire.

**Contains:**
- OpenTelemetry configuration
- Health check defaults
- Service discovery configuration
- HTTP resilience policies

**Naming:** `AspireWebAppTemplate.ServiceDefaults`

### Tests (all tests — references everything)

**Contains:**
- Property-based tests (FsCheck)
- Unit tests (xUnit + Moq)
- Integration tests (Aspire.Hosting.Testing)
- Organized by feature domain

**Naming:** `AspireWebAppTemplate.Tests`

---

## 5. Key Design Decisions

### Decision 1: Entity placement follows Identity-dependency rule

**Rule:** An entity belongs in **Domain** if it has no relationship (foreign key or navigation property) to Identity types (`ApplicationUser`, `ApplicationRole`). An entity belongs in **Infrastructure** if it references Identity.

**Current entity placement:**

| Entity | Location | Reason |
|--------|----------|--------|
| `EmailTemplate` | Domain | No FK to user/role |
| `Notification` | Infrastructure | Has `UserId` FK + `User` nav property |
| `NotificationPreference` | Infrastructure | Has `UserId` FK + `User` nav property |
| `Announcement` | Infrastructure | Has `CreatedByUserId` FK + `CreatedByUser` nav property |
| `AnnouncementDismissal` | Infrastructure | Has `UserId` FK + `User` nav property |
| `AuditLogEntry` | Infrastructure | Has `UserId` FK + `User` nav property |
| `PagePermission` | Infrastructure | Has `RoleId` FK + `Role` nav property |
| `ApplicationUser` | Infrastructure/Identity | IS an Identity type |
| `ApplicationRole` | Infrastructure/Identity | IS an Identity type |

**Rationale:** This is standard Clean Architecture practice — Domain entities are framework-agnostic and have no dependency on ASP.NET Core Identity. Infrastructure entities that reference Identity can use ordinary navigation properties and `.Include()` without workarounds.

**For future business entities:** Entities like `Product`, `Order`, `Invoice` — which have no relationship to Identity — naturally go into Domain. This is where the Domain project earns its value as business logic grows.

**Domain entities that need a user reference (rare case):** Use a plain `string` or `Guid` field (scalar FK) without a navigation property. Configure the relationship in Infrastructure via `builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(...)`. Only do this if the entity is otherwise a strong Domain candidate.

### Decision 2: DTOs live in Application, not Domain

**Rationale:** DTOs are API contracts — they describe use case inputs and outputs. They change when the API surface changes, not when domain vocabulary changes. Application is the "use case" layer; DTOs describe use case communication.

Web references Application to share these contract types. This is the standard Clean Architecture approach.

### Decision 3: Web references Application (not just Domain)

**Rationale:** Web needs DTOs to communicate with ApiService over HTTP. DTOs live in Application. Therefore Web references Application.

Web seeing service interfaces (e.g., `IUserService`) in Application is not a coupling concern — Web cannot *resolve* these interfaces because no implementation is registered in Web's DI container. It's visibility without coupling.

### Decision 4: No separate Domain entities project

**Rationale:** The design document (§3) proposed splitting entities between Domain (identity-free) and Infrastructure (identity-referencing). In practice, the document itself acknowledged most entities would end up in Infrastructure.

A near-empty Domain/Entities folder is a signal the abstraction isn't paying for itself. Keep all entities in Infrastructure. If a genuinely infrastructure-agnostic behavioral entity emerges, add it to Domain at that time.

### Decision 5: No InternalsVisibleTo enforcement

**Rationale:** If you need `InternalsVisibleTo` to prevent architectural violations, the boundaries are wrong. Correct project references make invalid dependencies a compiler error without any tricks.

Domain types are `public`. The Web project transitively sees Domain types through Application — this is fine and expected. Enums in Domain are used in DTOs in Application which Web needs.

### Decision 6: No Platform/Business folder split

**Rationale:** The Platform/Business split in the design document assumes a template-update workflow where platform folders are wholesale-replaced. This contradicts the copy-and-rename workflow where the template is forked and diverges.

Template updates are handled via Git merging (cherry-pick or merge from upstream), not folder replacement. The split adds navigation depth at every level without delivering its intended benefit.

### Decision 7: No fixed-identity projects

**Rationale:** All projects follow the same naming convention and are renamed during copy-and-rename. No `Enterprise.AspireWebAppPlatform.*` prefix on UI or ServiceDefaults.

This keeps the solution uniform, avoids confusing developers about why some projects have different naming, and doesn't imply a framework/SDK relationship.

### Decision 8: No MediatR, domain events, or CQRS

**Rationale:** Cross-cutting concerns (audit logging, notifications) are handled via direct service-to-service calls. At the current scale (~16 services, ~5 cross-cutting triggers), this is simpler and more debuggable than event dispatch.

**When to reconsider:** If cross-cutting triggers exceed ~15, or if services develop complex multi-step orchestrations where event ordering matters.

### Decision 9: No repository pattern

**Rationale:** EF Core's `DbContext` is already a Unit of Work with repository capabilities (DbSet). Wrapping it adds abstraction without benefit. Services access `ApplicationDbContext` directly in Infrastructure.

### Decision 10: Service interfaces in Application, implementations in Infrastructure

**Rationale:** This is the core of dependency inversion. Application defines *what* the system can do (`IUserService.CreateAsync()`). Infrastructure defines *how* (`UserService` uses `UserManager`, `DbContext`, audit logging). The composition root (ApiService's `Program.cs`) wires them together.

This enables:
- Unit testing Application-layer logic without EF Core (if pure application services emerge)
- Swapping Infrastructure implementations (different email provider, different ORM) without touching business contracts
- Clear "where does new code go?" guidance

---

## 6. Folder Structure Within Projects

### Domain

```
AspireWebAppTemplate.Domain/
├── Entities/
│   └── EmailTemplate.cs                  ← Identity-free entity (no user/role FK)
├── Enums/
│   ├── AnnouncementDisplayType.cs
│   ├── AnnouncementSeverity.cs
│   ├── AuditActionType.cs
│   ├── AuditEntityType.cs
│   ├── AuthSource.cs
│   ├── EmailTemplateCategory.cs
│   ├── EmailType.cs
│   ├── ExportScope.cs
│   ├── NotificationCategory.cs
│   └── ThemePreference.cs
├── ValueObjects/
│   └── (future use)
├── Constants/
│   ├── SystemPageDefaults.cs
│   ├── DateTimeFormatDefaults.cs
│   └── ExportDefaults.cs
└── Attributes/
    ├── ExportColumnAttribute.cs
    └── OptionalPhoneAttribute.cs
```

### Application

```
AspireWebAppTemplate.Application/
├── Abstractions/
│   ├── IAiService.cs
│   ├── IAnnouncementService.cs
│   ├── IAuditLogService.cs
│   ├── IAuthService.cs
│   ├── ICurrentUserAccessor.cs
│   ├── IEmailService.cs
│   ├── IEmailTemplateService.cs
│   ├── IExcelExportService.cs
│   ├── ILdapAuthService.cs
│   ├── ILdapLoginService.cs
│   ├── ILoginService.cs
│   ├── INavigationProvider.cs
│   ├── INavigationService.cs
│   ├── INotificationService.cs
│   ├── IPagePermissionService.cs
│   ├── IRegisterService.cs
│   ├── IRoleService.cs
│   ├── ITimeZoneService.cs
│   └── IUserService.cs
├── Contracts/
│   ├── Ai/
│   │   ├── AiPromptRequest.cs
│   │   └── AiResponseDto.cs
│   ├── Announcements/
│   │   ├── AnnouncementDto.cs
│   │   ├── AnnouncementQueryParams.cs
│   │   ├── CreateAnnouncementRequest.cs
│   │   └── UpdateAnnouncementRequest.cs
│   ├── AuditLog/
│   │   ├── AuditLogEntryDto.cs
│   │   ├── AuditLogQueryParams.cs
│   │   └── AuditLogRequest.cs
│   ├── Auth/
│   │   └── (all auth DTOs)
│   ├── Email/
│   │   ├── EmailTemplateDto.cs
│   │   ├── PreviewTemplateRequest.cs
│   │   ├── RenderedEmailResult.cs
│   │   └── UpdateEmailTemplateRequest.cs
│   ├── Notifications/
│   │   ├── BulkDismissRequest.cs
│   │   ├── CreateNotificationRequest.cs
│   │   ├── NotificationDto.cs
│   │   ├── NotificationPreferenceDto.cs
│   │   ├── NotificationPushRequest.cs
│   │   ├── NotificationQueryParams.cs
│   │   └── UpdateNotificationPreferenceRequest.cs
│   ├── PagePermissions/
│   │   ├── PagePermissionDto.cs
│   │   ├── RolePermissionsDto.cs
│   │   └── UpdateRolePermissionsRequest.cs
│   ├── Roles/
│   │   ├── CreateRoleRequest.cs
│   │   ├── RoleAssignmentResult.cs
│   │   └── RoleDto.cs
│   └── Users/
│       ├── AdminResetPasswordRequest.cs
│       ├── CreateUserRequest.cs
│       ├── LdapAuthResult.cs
│       ├── LdapSyncProgressItem.cs
│       ├── LdapUserAttributes.cs
│       ├── UpdatePreferencesRequest.cs
│       ├── UpdateProfileRequest.cs
│       ├── UpdateUserRequest.cs
│       └── UserDto.cs
├── Common/
│   ├── ApiResult.cs
│   ├── NavItem.cs
│   └── PagedResult.cs
└── Extensions/
    ├── NavigationProviderExtensions.cs
    └── QueryableExtensions.cs
```

### Infrastructure

```
AspireWebAppTemplate.Infrastructure/
├── Data/
│   ├── ApplicationDbContext.cs
│   ├── Entities/
│   │   ├── Announcement.cs              ← Has CreatedByUserId FK to ApplicationUser
│   │   ├── AnnouncementDismissal.cs     ← Has UserId FK to ApplicationUser
│   │   ├── AuditLogEntry.cs             ← Has UserId FK to ApplicationUser
│   │   ├── Notification.cs             ← Has UserId FK to ApplicationUser
│   │   ├── NotificationPreference.cs   ← Has UserId FK to ApplicationUser
│   │   └── PagePermission.cs           ← Has RoleId FK to ApplicationRole
│   ├── Configurations/
│   │   ├── AnnouncementConfiguration.cs
│   │   ├── AnnouncementDismissalConfiguration.cs
│   │   ├── AuditLogEntryConfiguration.cs
│   │   ├── EmailTemplateConfiguration.cs   ← Configures Domain entity (EmailTemplate)
│   │   ├── NotificationConfiguration.cs
│   │   ├── NotificationPreferenceConfiguration.cs
│   │   └── PagePermissionConfiguration.cs
│   ├── Migrations/
│   │   └── (all migration files)
│   └── SeedData/
│       ├── SeedData.cs
│       ├── SeedData.Announcements.cs
│       ├── SeedData.EmailTemplates.cs
│       ├── SeedData.PagePermissions.cs
│       ├── SeedData.Roles.cs
│       └── SeedData.Users.cs
├── Identity/
│   ├── ApplicationUser.cs
│   └── ApplicationRole.cs
├── Services/
│   ├── AiService.cs
│   ├── AnnouncementService.cs
│   ├── AuditLogService.cs
│   ├── AuthService.cs
│   ├── EmailService.cs
│   ├── EmailTemplateService.cs
│   ├── ExcelExportService.cs
│   ├── LdapAuthService.cs
│   ├── LdapLoginService.cs
│   ├── LoginService.cs
│   ├── NavigationService.cs
│   ├── NotificationService.cs
│   ├── PagePermissionService.cs
│   ├── RegisterService.cs
│   ├── RoleService.cs
│   ├── TimeZoneService.cs
│   ├── DefaultNavigationProvider.cs
│   └── UserService.cs
├── Clients/
│   └── WebCallbackClient.cs
├── Handlers/
│   └── InternalApiKeyDelegatingHandler.cs
├── Utilities/
│   ├── AuditChangeHelper.cs
│   ├── CurrentUserAccessor.cs
│   └── SecureConnectionString.cs
└── Extensions/
    └── InfrastructureServiceExtensions.cs
```

### ApiService

```
AspireWebAppTemplate.ApiService/
├── Controllers/
│   ├── AiController.cs
│   ├── AnnouncementController.cs
│   ├── AuditLogController.cs
│   ├── AuthController.cs
│   ├── BaseController.cs
│   ├── EmailTemplateController.cs
│   ├── NavigationController.cs
│   ├── NotificationController.cs
│   ├── PagePermissionsController.cs
│   ├── RolesController.cs
│   ├── UsersController.cs
│   └── WeatherController.cs
├── Authentication/
│   └── InternalAuthenticationHandler.cs
├── Program.cs
├── appsettings.json
└── appsettings.Development.json
```

---

## 7. Project References (.csproj)

### Domain.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <!-- Zero dependencies -->
</Project>
```

### Application.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\AspireWebAppTemplate.Domain\AspireWebAppTemplate.Domain.csproj" />
  </ItemGroup>
  <!-- Zero NuGet package dependencies -->
</Project>
```

### Infrastructure.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\AspireWebAppTemplate.Application\AspireWebAppTemplate.Application.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" />
    <PackageReference Include="AWSSDK.BedrockRuntime" />
    <PackageReference Include="EPPlus" />
    <PackageReference Include="HtmlSanitizer" />
    <PackageReference Include="System.DirectoryServices" />
    <PackageReference Include="System.DirectoryServices.Protocols" />
  </ItemGroup>
</Project>
```

### ApiService.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\AspireWebAppTemplate.Application\AspireWebAppTemplate.Application.csproj" />
    <ProjectReference Include="..\AspireWebAppTemplate.Infrastructure\AspireWebAppTemplate.Infrastructure.csproj" />
    <ProjectReference Include="..\AspireWebAppTemplate.ServiceDefaults\AspireWebAppTemplate.ServiceDefaults.csproj" />
  </ItemGroup>
</Project>
```

### Web.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\AspireWebAppTemplate.Application\AspireWebAppTemplate.Application.csproj" />
    <ProjectReference Include="..\AspireWebAppTemplate.UI\AspireWebAppTemplate.UI.csproj" />
    <ProjectReference Include="..\AspireWebAppTemplate.ServiceDefaults\AspireWebAppTemplate.ServiceDefaults.csproj" />
  </ItemGroup>
</Project>
```

---

## 8. Migration Mapping (Current → New)

| Current Location | New Location |
|-----------------|--------------|
| `Core/Domain/Enums/` | `Domain/Enums/` |
| `Core/Domain/ValueObjects/` | `Domain/ValueObjects/` |
| `Core/Common/Defaults/` | `Domain/Constants/` |
| `Core/Utilities/Attributes/` | `Domain/Attributes/` |
| `Core/Contracts/` | `Application/Contracts/` |
| `Core/Common/ApiResult.cs` | `Application/Common/ApiResult.cs` |
| `Core/Common/NavItem.cs` | `Application/Common/NavItem.cs` |
| `Core/Contracts/PagedResult.cs` | `Application/Common/PagedResult.cs` |
| `Core/Application/Abstractions/` | `Application/Abstractions/` |
| `Core/Application/Services/` | `Infrastructure/Services/` |
| `Core/Extensions/` | `Application/Extensions/` |
| `Core/Utilities/SecureConnectionString.cs` | `Infrastructure/Utilities/SecureConnectionString.cs` |
| `ApiService/Abstractions/` | `Application/Abstractions/` |
| `ApiService/Services/` | `Infrastructure/Services/` |
| `ApiService/Services/Infrastructure/` | `Infrastructure/Utilities/` |
| `ApiService/Services/Clients/` | `Infrastructure/Clients/` |
| `ApiService/Services/Handlers/` | `Infrastructure/Handlers/` |
| `ApiService/Data/` | `Infrastructure/Data/` |
| `ApiService/Data/Entities/` | `Infrastructure/Data/Entities/` + `Infrastructure/Identity/` |
| `ApiService/Data/Entities/EmailTemplate.cs` | `Domain/Entities/EmailTemplate.cs` |
| `ApiService/Controllers/` | `ApiService/Controllers/` (stays) |
| `ApiService/Authentication/` | `ApiService/Authentication/` (stays) |
| `ApiService/Extensions/` | `Infrastructure/Extensions/` (DI registration) |

---

## 9. Implementation Impact Analysis

The migration is fundamentally a **reorganization, not a rewrite**. Existing code moves between projects with namespace changes but minimal logic changes.

### Code That Stays Exactly The Same (Just Moved)

| Code | Why unchanged |
|------|--------------|
| Service implementations (e.g., `NotificationService`) | Same DbContext usage, same logic, new namespace |
| Controllers | Same thin-controller pattern, same exception-to-status mapping |
| EF Configurations | Same `IEntityTypeConfiguration<T>`, new namespace |
| DTOs / Contracts | Same shape, new namespace |
| Entities (Infrastructure) | Same properties, same nav properties, new namespace |
| Web typed HTTP clients | Same HTTP calls, same DTO references |
| Per-circuit contexts | Same pattern, same Application references |
| Tests | Same logic, updated `using` statements |

### Code That Actually Changes

| Change | Description | Effort |
|--------|-------------|--------|
| **Namespace updates** | All `using AspireWebAppTemplate.ApiService.*` become `using AspireWebAppTemplate.Infrastructure.*` (or Application). IDE refactoring handles this. | Mechanical / automated |
| **DI registration method** | `ApiService/Extensions/ApplicationServiceExtensions.cs` moves to `Infrastructure/Extensions/InfrastructureServiceExtensions.cs`. Method name changes from `AddApplicationServices()` to `AddInfrastructureServices()`. | Rename + move |
| **ApiService Program.cs** | `builder.Services.AddApplicationServices()` becomes `builder.Services.AddInfrastructureServices()` | One line change |
| **EF Core migration assembly** | Since `ApplicationDbContext` moves from ApiService to Infrastructure, migrations need: `options.UseSqlServer(conn, b => b.MigrationsAssembly("AspireWebAppTemplate.Infrastructure"))` | One config line |
| **Domain entity (EmailTemplate)** | Loses its `using AspireWebAppTemplate.ApiService.Data.Entities` namespace. Its EF Configuration in Infrastructure references it from Domain. No navigation property changes needed (it has none to Identity). | Namespace only |
| **Infrastructure.csproj needs FrameworkReference** | Infrastructure needs `<FrameworkReference Include="Microsoft.AspNetCore.App" />` because services use `ILogger<T>`, `IHttpContextAccessor`, etc. from ASP.NET Core. | One line in csproj |
| **Tests project references** | Replace `ProjectReference` to ApiService with references to Application + Infrastructure (for unit tests). Keep ApiService reference for integration tests. | Update csproj |

### Code Patterns That Do NOT Change

- Services still inject `ApplicationDbContext` directly (no repository layer)
- Services still throw `KeyNotFoundException`/`InvalidOperationException` for business rules
- Controllers still use try/catch to map exceptions to HTTP status codes
- `ICurrentUserAccessor` pattern stays identical
- Audit logging via `IAuditLogService.LogAsync()` stays identical
- Cross-cutting notification creation stays identical
- `WebCallbackClient` real-time delivery pattern stays identical
- Web ↔ ApiService HTTP communication stays identical
- Internal API key authentication stays identical

### Summary

~95% of the migration is file moves + namespace updates (automatable via IDE refactoring). The remaining ~5% is configuration adjustments (csproj references, Program.cs wiring, migration assembly). No business logic rewrites required.

---

## 10. What Does NOT Change

- The copy-and-rename distribution workflow
- .NET Aspire orchestration (AppHost composition)
- The Web ↔ ApiService HTTP communication boundary
- The thin-controller / full-service-layer pattern
- MudBlazor / Blazor Server patterns
- Test organization by feature domain
- Naming conventions (PascalCase classes, kebab-case features)
- Region structure in services and controllers
- XML documentation standards
- DateTime conventions (UTC, `Utc` suffix)
- Seed data patterns (upsert, partial classes)
- The UI project's role and contents
- ServiceDefaults contents

---

## 11. When to Evolve Further

| Trigger | Action |
|---------|--------|
| Entities gain behavioral methods (invariants, state machines) | Extract those entities to Domain |
| Cross-cutting triggers exceed ~15 | Introduce `IEventPublisher` in Application, implementation in Infrastructure |
| Need for CQRS (separate read/write models) | Add read-specific DTOs in Application, separate query services |
| Team grows beyond 5 concurrent developers | Consider splitting Infrastructure by feature into focused projects |
| Multiple API protocols needed (REST + gRPC + GraphQL) | Keep Application unchanged; add new host projects alongside ApiService |

---

## 12. Superseded Document

This document supersedes `AspireWebAppTemplate-CleanArchitecture-Design.md`, which explored a four-project Clean Architecture with Platform/Business splits, InternalsVisibleTo enforcement, and fixed-identity projects. Those approaches were evaluated and rejected for the reasons documented in Decision 6, Decision 5, and Decision 7 above.
