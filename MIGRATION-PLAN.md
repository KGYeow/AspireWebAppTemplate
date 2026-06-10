# Migration Plan: BlazorWebAppTemplate → AspireWebAppTemplate

## Overview

This document describes the migration of all features from the single-tier `BlazorWebAppTemplate` (Blazor Server monolith) to the multi-tier `AspireWebAppTemplate` (.NET Aspire with separated frontend + API backend).

**Source project:** `c:\Users\4093094\source\repos\BlazorWebAppTemplate` (DO NOT DELETE — serves as backup)  
**Destination project:** `c:\Users\4093094\source\repos\AspireWebAppTemplate`

---

## Architecture Change

```
BEFORE (BlazorWebAppTemplate — single process):
  Browser ←SignalR→ [Blazor Server: Pages + Services + DbContext] → SQL Server

AFTER (AspireWebAppTemplate — 3-tier):
  Browser ←SignalR→ [Blazor Web Frontend] ←HTTP→ [API Service: Controllers + Services + DbContext] → SQL Server
                         ↑                              ↑
                    AppHost orchestrates both via Aspire service discovery
```

---

## Final Project Structure

```
AspireWebAppTemplate/
├── AspireWebAppTemplate.AppHost/              ← Aspire orchestrator (already exists)
│   └── AppHost.cs                             ← Defines services, DB, references
│
├── AspireWebAppTemplate.ServiceDefaults/      ← Shared Aspire config (already exists)
│   └── Extensions.cs                          ← Health checks, telemetry, resilience
│
├── AspireWebAppTemplate.ApiService/           ← Backend API (business logic + data access)
│   ├── Controllers/                           ← NEW: API controllers
│   │   ├── AuthController.cs                  ← Login, logout, register endpoints
│   │   ├── UserManagementController.cs        ← User CRUD endpoints
│   │   ├── RoleManagementController.cs        ← Role CRUD endpoints
│   │   └── AuditLogController.cs             ← Audit log query + export endpoints
│   ├── Data/                                  ← NEW: EF Core data layer
│   │   ├── ApplicationDbContext.cs
│   │   ├── Entities/                          ← ApplicationUser, ApplicationRole, AuditLogEntry
│   │   ├── Migrations/                        ← EF Core migrations
│   │   └── SeedData.cs
│   ├── Services/                              ← NEW: Business logic services
│   │   ├── LoginService.cs
│   │   ├── RegisterService.cs
│   │   ├── LdapAuthService.cs
│   │   ├── LdapLoginService.cs
│   │   ├── AuditLogService.cs
│   │   └── ExcelExportService.cs
│   ├── Abstractions/                          ← NEW: Service interfaces
│   │   ├── ILoginService.cs
│   │   ├── IRegisterService.cs
│   │   ├── ILdapAuthService.cs
│   │   ├── ILdapLoginService.cs
│   │   ├── IAuditLogService.cs
│   │   └── IExcelExportService.cs
│   ├── Options/                               ← NEW: Configuration classes
│   │   └── LdapSettings.cs
│   ├── Contracts/                             ← NEW: Request/Response DTOs for API
│   │   ├── LoginRequest.cs
│   │   ├── LoginResponse.cs
│   │   ├── UserDto.cs
│   │   ├── RoleDto.cs
│   │   ├── AuditLogEntryDto.cs
│   │   └── PagedResult.cs
│   ├── Program.cs                             ← Identity + EF Core + DI registration
│   └── appsettings.json                       ← Connection strings, LDAP, AuditLog, EPPlus config
│
├── AspireWebAppTemplate.Web/                  ← Blazor frontend (presentation only)
│   ├── Components/
│   │   ├── Layout/                            ← Main layout, nav menu (from BlazorWebAppTemplate)
│   │   ├── Pages/                             ← All Razor pages (migrated)
│   │   │   ├── Account/                       ← Login, Register, Manage pages
│   │   │   ├── UserManagement/                ← Admin user CRUD UI
│   │   │   ├── RoleManagement/                ← Admin role CRUD UI
│   │   │   ├── AuditLog/                      ← Audit log grid + detail dialog
│   │   │   └── Settings/                      ← User settings page
│   │   ├── Shared/                            ← Shared UI components
│   │   ├── App.razor
│   │   ├── Routes.razor
│   │   └── _Imports.razor
│   ├── Services/                              ← NEW: All frontend services
│   │   ├── ApiAuthService.cs                  ← Calls AuthController via HTTP
│   │   ├── ApiUserManagementService.cs        ← Calls UserManagementController via HTTP
│   │   ├── ApiRoleManagementService.cs        ← Calls RoleManagementController via HTTP
│   │   ├── ApiAuditLogService.cs             ← Calls AuditLogController via HTTP
│   │   ├── ThemeStateService.cs               ← Manages UI theme state (frontend-only, no API)
│   │   └── UserTimeZoneContext.cs             ← Formats UTC timestamps in user's timezone (frontend-only, no API)
│   ├── Abstractions/                          ← NEW: Frontend service interfaces
│   │   ├── IThemeStateService.cs              ← Theme switching contract
│   │   └── IUserTimeZoneContext.cs            ← Timezone formatting contract
│   ├── wwwroot/                               ← Static assets (CSS, JS, images)
│   ├── Program.cs                             ← MudBlazor + HttpClient + auth middleware
│   └── appsettings.json
├── AspireWebAppTemplate.Core/                 ← NEW: Shared domain models (referenced by both)
│   ├── Domain/
│   │   ├── Enums/                             ← AuditActionType, AuditEntityType, ExportScope, AuthSource
│   │   └── Models/                            ← Shared value objects
│   ├── Utilities/                             ← ExportColumnAttribute, SecureConnectionString
│   └── Common/                                ← NavModels, shared constants
│
├── AspireWebAppTemplate.UI/                   ← NEW: Shared UI components library
│   ├── Components/
│   │   ├── DataGrid/                          ← BoolFilterSelect, etc.
│   │   └── Shared/                            ← ConfirmationDialog, PageHeader, etc.
│   ├── Utilities/                             ← DataGridUtils<T>, QueryableDataGridUtils<T>
│   ├── Theme/                                 ← ApplicationTheme.cs
│   └── wwwroot/                               ← Shared static assets
│
├── AspireWebAppTemplate.Tests/                ← Tests (already exists, will be expanded)
│   ├── AuditLog/                              ← Property-based tests
│   ├── Integration/                           ← Aspire integration tests
│   └── WebTests.cs                            ← Existing Aspire web test
│
└── MIGRATION-PLAN.md                          ← This document
```

---

## Migration Map: What → Where → How

### Phase 1: Core & UI Libraries (Direct Copy)

These files can be **directly copied** with minimal changes (just namespace updates):

| Source (BlazorWebAppTemplate) | Destination (AspireWebAppTemplate) | Changes Needed |
|-------------------------------|-------------------------------------|----------------|
| `BlazorWebAppTemplate.Core/` (entire project) | `AspireWebAppTemplate.Core/` | Rename namespace `BlazorWebAppTemplate.Core` → `AspireWebAppTemplate.Core` |
| `BlazorWebAppTemplate.UI/` (entire project) | `AspireWebAppTemplate.UI/` | Rename namespace `BlazorWebAppTemplate.UI` → `AspireWebAppTemplate.UI` |

**Files to copy directly (just namespace find/replace):**
- `Core/Domain/Enums/*.cs` — all enum files
- `Core/Utilities/ExportColumnAttribute.cs`
- `Core/Utilities/SecureConnectionString.cs`
- `Core/Common/NavModels.cs`
- `Core/Application/Services/DefaultNavigationProvider.cs`
- `Core/Application/Abstractions/INavigationProvider.cs`
- `UI/Utilities/DataGridUtils.cs`
- `UI/Utilities/QueryableDataGridUtils.cs`
- `UI/Utilities/DisplayHelper.cs`
- `UI/Theme/ApplicationTheme.cs`
- `UI/Components/**` — all shared components

### Phase 2: Data Layer (Copy to ApiService)

| Source | Destination | Changes |
|--------|-------------|---------|
| `BlazorWebAppTemplate/Data/ApplicationDbContext.cs` | `ApiService/Data/ApplicationDbContext.cs` | Namespace change |
| `BlazorWebAppTemplate/Data/Entities/*.cs` | `ApiService/Data/Entities/*.cs` | Namespace change |
| `BlazorWebAppTemplate/Data/SeedData.cs` | `ApiService/Data/SeedData.cs` | Namespace change |
| `BlazorWebAppTemplate/Data/Migrations/` | `ApiService/Data/Migrations/` | Regenerate fresh migrations (recommended) |

### Phase 3: Services & Abstractions (Copy to ApiService)

**Direct copy (namespace change only):**

| Source | Destination |
|--------|-------------|
| `Abstractions/IAuditLogService.cs` | `ApiService/Abstractions/IAuditLogService.cs` |
| `Abstractions/IExcelExportService.cs` | `ApiService/Abstractions/IExcelExportService.cs` |
| `Abstractions/ILoginService.cs` | `ApiService/Abstractions/ILoginService.cs` |
| `Abstractions/IRegisterService.cs` | `ApiService/Abstractions/IRegisterService.cs` |
| `Abstractions/ILdapAuthService.cs` | `ApiService/Abstractions/ILdapAuthService.cs` |
| `Abstractions/ILdapLoginService.cs` | `ApiService/Abstractions/ILdapLoginService.cs` |
| `Abstractions/IThemeStateService.cs` | `Web/Abstractions/IThemeStateService.cs` (frontend-only) |
| `Abstractions/IUserTimeZoneContext.cs` | `Web/Abstractions/IUserTimeZoneContext.cs` (frontend-only) |
| `Services/ThemeStateService.cs` | `Web/Services/ThemeStateService.cs` (frontend-only, no API) |
| `Services/UserTimeZoneContext.cs` | `Web/Services/UserTimeZoneContext.cs` (frontend-only, no API) |
| `Services/AuditLogService.cs` | `ApiService/Services/AuditLogService.cs` |
| `Services/ExcelExportService.cs` | `ApiService/Services/ExcelExportService.cs` |
| `Services/LoginService.cs` | `ApiService/Services/LoginService.cs` |
| `Services/RegisterService.cs` | `ApiService/Services/RegisterService.cs` |
| `Services/LdapAuthService.cs` | `ApiService/Services/LdapAuthService.cs` |
| `Services/LdapLoginService.cs` | `ApiService/Services/LdapLoginService.cs` |
| `Options/LdapSettings.cs` | `ApiService/Options/LdapSettings.cs` |

### Phase 4: API Controllers (New — wraps existing services)

These are **new files** that expose service methods as HTTP endpoints:

| Controller | Wraps | Key Endpoints |
|-----------|-------|---------------|
| `AuthController.cs` | `ILoginService`, `ILdapLoginService`, `IRegisterService` | POST /api/auth/login, POST /api/auth/register, POST /api/auth/logout |
| `UserManagementController.cs` | `UserManager<ApplicationUser>` | GET /api/users, POST /api/users, PUT /api/users/{id}, DELETE /api/users/{id}, POST /api/users/{id}/activate |
| `RoleManagementController.cs` | `RoleManager<ApplicationRole>` | GET /api/roles, POST /api/roles, PUT /api/roles/{id}, DELETE /api/roles/{id}, POST /api/roles/{id}/assign |
| `AuditLogController.cs` | `IAuditLogService`, `IExcelExportService` | GET /api/audit-log (paged), GET /api/audit-log/{id}, GET /api/audit-log/export |

### Phase 5: Frontend Pages (Copy + Refactor)

Razor pages can be **mostly copied** but need refactoring:
- Remove direct `[Inject] ApplicationDbContext` — replace with HTTP client calls
- Remove direct `[Inject] UserManager<>` — replace with API client service
- Keep MudBlazor components, layout, and UI logic as-is

| Source | Destination | Refactoring |
|--------|-------------|-------------|
| `Components/Pages/UserManagement/` | `Web/Components/Pages/UserManagement/` | Replace DbContext/UserManager with `ApiUserManagementService` |
| `Components/Pages/RoleManagement/` | `Web/Components/Pages/RoleManagement/` | Replace RoleManager with `ApiRoleManagementService` |
| `Components/Pages/AuditLog/` | `Web/Components/Pages/AuditLog/` | Replace DbContext with `ApiAuditLogService` |
| `Components/Account/Pages/` | `Web/Components/Pages/Account/` | Replace LoginService with `ApiAuthService` |
| `Components/Layout/` | `Web/Components/Layout/` | Direct copy (MudBlazor layout) |
| `wwwroot/` | `Web/wwwroot/` | Direct copy (CSS, JS, images) |

### Phase 6: Frontend HTTP Client Services (New)

These are **new wrapper services** in the Web project that call the API via HttpClient:

```csharp
// Example: ApiAuditLogService.cs
public class ApiAuditLogService(HttpClient http)
{
    public async Task<PagedResult<AuditLogEntryDto>> GetPagedAsync(AuditLogQueryParams query)
        => await http.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>($"/api/audit-log?{query.ToQueryString()}");

    public async Task<byte[]> ExportExcelAsync(AuditLogQueryParams query)
        => await http.GetByteArrayAsync($"/api/audit-log/export?{query.ToQueryString()}");
}
```

### Phase 7: Configuration & Wiring

| Item | Location | Notes |
|------|----------|-------|
| Connection strings | `ApiService/appsettings.json` | Same as BlazorWebAppTemplate |
| LDAP settings | `ApiService/appsettings.json` | Same |
| AuditLog:RetentionDays | `ApiService/appsettings.json` | Same |
| EPPlus license | `ApiService/appsettings.json` | Same |
| Identity registration | `ApiService/Program.cs` | `AddIdentity`, `AddEntityFrameworkStores` |
| MudBlazor | `Web/Program.cs` | `AddMudServices` |
| HttpClient setup | `Web/Program.cs` | Aspire service discovery (`https+http://apiservice`) |
| AppHost wiring | `AppHost/AppHost.cs` | Add SQL Server resource, wire to apiservice |

### Phase 8: Tests (Copy + Update)

| Source | Destination | Notes |
|--------|-------------|-------|
| `BlazorWebAppTemplate.Tests/AuditLog/` | `AspireWebAppTemplate.Tests/AuditLog/` | Namespace change, reference ApiService instead |
| New integration tests | `AspireWebAppTemplate.Tests/Integration/` | Test API endpoints via `DistributedApplicationTestingBuilder` |

---

## Files You Can Directly Copy/Paste (Namespace Replace Only)

These files need only a find/replace of `BlazorWebAppTemplate` → `AspireWebAppTemplate`:

### Core project (entire folder):
- All files in `Core/Domain/Enums/`
- All files in `Core/Utilities/`
- All files in `Core/Common/`
- All files in `Core/Application/`

### UI project (entire folder):
- All files in `UI/Utilities/`
- All files in `UI/Theme/`
- All files in `UI/Components/`
- `UI/wwwroot/` (no namespace in static assets)

### Data layer:
- `Data/Entities/ApplicationUser.cs`
- `Data/Entities/ApplicationRole.cs`
- `Data/Entities/AuditLogEntry.cs`
- `Data/ApplicationDbContext.cs`
- `Data/SeedData.cs`

### Services (no logic change, just namespace):
- All files in `Services/` (except ThemeStateService and UserTimeZoneContext → go to Web/LocalServices/)
- All files in `Abstractions/` (except IThemeStateService and IUserTimeZoneContext → go to Web/Abstractions/)
- All files in `Options/`

### Frontend-only services (direct copy, just namespace):
- `Services/ThemeStateService.cs` → `Web/Services/ThemeStateService.cs`
- `Services/UserTimeZoneContext.cs` → `Web/Services/UserTimeZoneContext.cs`
- `Abstractions/IThemeStateService.cs` → `Web/Abstractions/IThemeStateService.cs`
- `Abstractions/IUserTimeZoneContext.cs` → `Web/Abstractions/IUserTimeZoneContext.cs`

### Static assets:
- Entire `wwwroot/` folder (CSS, JS, images, favicon)

---

## Files That Need Significant Refactoring

These **cannot** be directly copied — they need architectural changes:

| File | Why |
|------|-----|
| `Components/Pages/UserManagement/Index.razor.cs` | Currently injects `UserManager` directly. Needs to call API instead. |
| `Components/Pages/RoleManagement/Index.razor.cs` | Same — direct Identity access → API calls |
| `Components/Pages/AuditLog/Index.razor.cs` | Currently injects `ApplicationDbContext` directly → API calls |
| `Components/Account/Pages/Login.razor.cs` | Direct `ILoginService` injection → `ApiAuthService` HTTP calls |
| `Program.cs` (main app) | Splits into `ApiService/Program.cs` + `Web/Program.cs` |
| `Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs` | Logout endpoint moves to API |

---

## New Folders to Create

| Path | Purpose |
|------|---------|
| `AspireWebAppTemplate.Core/` | New project — shared domain models |
| `AspireWebAppTemplate.UI/` | New project — shared UI components |
| `AspireWebAppTemplate.ApiService/Controllers/` | API endpoint controllers |
| `AspireWebAppTemplate.ApiService/Data/` | EF Core context + entities |
| `AspireWebAppTemplate.ApiService/Data/Entities/` | Entity classes |
| `AspireWebAppTemplate.ApiService/Data/Migrations/` | EF Core migrations |
| `AspireWebAppTemplate.ApiService/Services/` | Business logic |
| `AspireWebAppTemplate.ApiService/Abstractions/` | Service interfaces |
| `AspireWebAppTemplate.ApiService/Options/` | Config option classes |
| `AspireWebAppTemplate.ApiService/Contracts/` | API DTOs (request/response models) |
| `AspireWebAppTemplate.Web/Services/` | All frontend services (API clients + local services) |
| `AspireWebAppTemplate.Web/Abstractions/` | Frontend service interfaces (IThemeStateService, IUserTimeZoneContext) |
| `AspireWebAppTemplate.Web/Components/Pages/Account/` | Auth pages |
| `AspireWebAppTemplate.Web/Components/Pages/UserManagement/` | User admin pages |
| `AspireWebAppTemplate.Web/Components/Pages/RoleManagement/` | Role admin pages |
| `AspireWebAppTemplate.Web/Components/Pages/AuditLog/` | Audit log page |
| `AspireWebAppTemplate.Web/Components/Pages/Settings/` | Settings page |
| `AspireWebAppTemplate.Web/Components/Shared/` | Shared UI components |
| `AspireWebAppTemplate.Tests/AuditLog/` | Property-based tests |
| `AspireWebAppTemplate.Tests/Integration/` | Aspire integration tests |

---

## Migration Order (Recommended)

1. **Create Core + UI projects** (direct copy, namespace replace)
2. **Set up ApiService data layer** (entities, DbContext, migration)
3. **Move services to ApiService** (direct copy, namespace replace)
4. **Create API controllers** (new code, wraps services)
5. **Wire up ApiService Program.cs** (Identity, EF Core, DI, auth)
6. **Set up Web frontend** (MudBlazor, layout, HttpClient)
7. **Create frontend API client services** (new code)
8. **Migrate pages** (copy Razor, refactor code-behind to use HTTP clients)
9. **Wire AppHost** (SQL Server, service references)
10. **Migrate tests** (namespace replace + add integration tests)

---

## Key Architectural Decisions

| Decision | Rationale |
|----------|-----------|
| Identity lives in ApiService | Single source of truth for auth; frontend gets tokens via API |
| QueryableDataGridUtils stays in UI project | It's a generic MudDataGrid utility — but in Aspire, the frontend won't have direct DB access. The API will handle paging/filtering and return DTOs. The utility moves to ApiService or becomes server-side controller logic. |
| ExcelExportService stays in ApiService | Export generation needs DB access; frontend requests bytes via API |
| Shared DTOs in Core or Contracts | API request/response models shared between frontend and API |
| MudBlazor stays in Web only | UI framework is frontend concern only |
| Audit logging stays in ApiService | Business logic layer responsibility |

---

## Notes

- **DO NOT delete** files from BlazorWebAppTemplate — it serves as reference/backup
- The Aspire AppHost orchestrates both services with service discovery (no hardcoded URLs)
- Authentication flow changes: Web frontend authenticates via API, stores JWT/cookie, passes to subsequent API calls
- The `QueryableDataGridUtils<T>` pattern changes — in Aspire, the API returns paged results and the frontend just displays them (no direct IQueryable from frontend)
