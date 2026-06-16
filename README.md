# AspireWebAppTemplate

A comprehensive .NET Aspire web application template built with .NET 10.0, featuring a multi-tier architecture with separated frontend and API backend, modern UI with MudBlazor, authentication (local Identity + LDAP), and full audit logging.

## Overview

This is a full-featured .NET Aspire application template designed to kickstart enterprise-level web applications with a clean 3-tier architecture. The frontend (Blazor Server) communicates with the backend (ASP.NET Core Web API) via HTTP, orchestrated by .NET Aspire for service discovery, health checks, and telemetry.

## Architecture

```
Browser ←SignalR→ [Web: Blazor Server + MudBlazor]
                         │
                    HttpClient (Aspire service discovery)
                         │
                         ↓
                  [ApiService: Controllers + Services + EF Core + Identity]
                         │
                         ↓
                    [SQL Server]
```

## Project Structure

The solution is organized into seven projects:

### 1. **AspireWebAppTemplate.AppHost** (Aspire Orchestrator)
Defines and orchestrates all services, databases, and their references using .NET Aspire.

### 2. **AspireWebAppTemplate.ServiceDefaults** (Shared Aspire Configuration)
Shared configuration for health checks, telemetry (OpenTelemetry), resilience, and service discovery.

### 3. **AspireWebAppTemplate.ApiService** (Backend API)
ASP.NET Core Web API with business logic, data access, and Identity:
- **Controllers/** — API endpoints (Auth, Users, Roles, AuditLog)
- **Data/** — EF Core DbContext, entities, migrations, seed data
- **Services/** — Business logic (Login, Register, LDAP, AuditLog, ExcelExport)
- **Abstractions/** — Service interfaces
- **Options/** — Configuration classes (LdapSettings)

### 4. **AspireWebAppTemplate.Web** (Frontend)
Blazor Server frontend (Global InteractiveServer mode):
- **Components/Pages/** — Application pages
  - `Account/Auth/` — Login, Register, ForgotPassword, ResetPassword, etc.
  - `Account/Manage/` — Profile, Email, ChangePassword, 2FA, Passkeys, etc.
  - `UserManagement/` — Admin user CRUD with bulk operations
  - `RoleManagement/` — Admin role CRUD
  - `AuditLog/` — Searchable audit log with export
  - `Settings/` — User preferences (theme, timezone, date format)
  - `Example/` — Demo pages (Auth, Counter, Weather)
- **Components/Layout/** — MainLayout, AuthLayout, ManageLayout, NavMenu, Topbar
- **Services/** — HTTP client services (ApiAuth, ApiUser, ApiRole, ApiAuditLog)
- **Abstractions/** — Frontend service interfaces

### 5. **AspireWebAppTemplate.Core** (Shared Domain)
Shared between frontend and backend:
- **Domain/Enums/** — Business enumerations (AuditActionType, AuthSource, ThemePreference)
- **Contracts/** — DTOs shared between API and frontend, organized by feature:
  - `Auth/` — Login, Register, Password, 2FA, Passkeys, External Logins
  - `Users/` — UserDto, CRUD requests, LDAP, Preferences, Profile
  - `Roles/` — RoleDto, CreateRoleRequest
  - `AuditLog/` — AuditLogEntryDto, QueryParams
  - `PagedResult.cs` — Generic paged response wrapper
- **Common/** — ApiResult<T>, ExportDefaults, NavModels, DateTimeFormatDefaults
- **Application/** — Navigation provider, TimeZoneService
- **Utilities/** — ExportColumnAttribute, SecureConnectionString, OptionalPhone

### 6. **AspireWebAppTemplate.UI** (Shared UI Components)
Reusable Blazor components and themes:
- **Components/Shared/** — ConfirmationDialog, PageHeader, ModalDialog
- **Components/DataGrid/** — BoolFilterSelect
- **Theme/** — ApplicationTheme
- **Utilities/** — DataGridUtils<T> (client-side filtering/sorting/pagination)

### 7. **AspireWebAppTemplate.Tests** (Test Project)
- **AuditLog/** — Property-based tests (FsCheck)
- Integration and unit tests

## Technology Stack

- **Framework**: .NET 10.0, .NET Aspire
- **Frontend**: Blazor Server (Global InteractiveServer)
- **UI Components**: MudBlazor 9.5.0
- **Backend**: ASP.NET Core Web API with Controllers
- **Database**: SQL Server with Entity Framework Core 10.0
- **Authentication**: Cookie-based (Web) + ASP.NET Core Identity (API)
- **LDAP**: Active Directory integration via System.DirectoryServices.Protocols
- **Excel Export**: EPPlus
- **Telemetry**: OpenTelemetry
- **Testing**: xUnit, FsCheck.Xunit, Moq

## Key Features

### Authentication
- **Local Identity**: Email/password with lockout, 2FA, recovery codes
- **LDAP Integration**: Active Directory authentication with auto-provisioning and attribute sync
- **Cookie-based auth**: Web project sets cookies after API validates credentials
- **Token exchange**: Single-use tokens in IMemoryCache for secure cross-service sign-in

### User & Role Management
- Full CRUD for users and roles with MudDataGrid
- Bulk operations (activate, deactivate, delete, assign roles)
- Position-based authority hierarchy
- LDAP user sync

### Audit Log
- Comprehensive audit trail for all user/role/auth operations
- Searchable, filterable data grid with server-side pagination
- Excel export
- Configurable retention

### Settings & Preferences
- Theme (Light/Dark/System)
- Timezone (auto-detect from browser)
- Date/time format
- Instant-save on change

### Architecture Benefits
- **Scalability**: Frontend and backend can scale independently
- **Service Discovery**: Aspire handles inter-service communication
- **Resilience**: Built-in retry and circuit breaker patterns
- **Observability**: OpenTelemetry tracing, metrics, and logging
- **Separation of Concerns**: Frontend has zero database/Identity dependencies

## Getting Started

### Prerequisites
- .NET 10.0 SDK
- Docker Desktop (for Aspire dashboard) or SQL Server (local/remote)
- Visual Studio 2022+ or VS Code with C# Dev Kit

### Setup Instructions

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd AspireWebAppTemplate
   ```

2. **Configure the database connection**
   Update `AspireWebAppTemplate.ApiService/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=AspireWebAppTemplateDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
     }
   }
   ```

3. **Run with Aspire (recommended)**
   ```bash
   dotnet run --project AspireWebAppTemplate.AppHost
   ```
   Aspire dashboard opens at `https://localhost:17024` with links to both services.

4. **Or run ApiService standalone** (for EF migrations)
   ```bash
   dotnet run --project AspireWebAppTemplate.ApiService
   ```

5. **Database migrations**
   - Auto-migrates on startup in Development mode
   - Manual: `Update-Database -Project AspireWebAppTemplate.ApiService -StartupProject AspireWebAppTemplate.ApiService`
   - CLI: `dotnet ef database update --project AspireWebAppTemplate.ApiService --startup-project AspireWebAppTemplate.ApiService`

6. **Default accounts** (seeded automatically)
   - Admin: `admin@example.com` / `Admin123#`
   - User: `user@example.com` / `User123#`

## Configuration

### ApiService/appsettings.json
- **ConnectionStrings** — SQL Server connection
- **LDAP** — Active Directory settings (Server, Port, BaseDn, Domain, Enabled)
- **AuditLog** — RetentionDays (default: 365)
- **EPPlus** — License configuration

### Web/appsettings.json
- Minimal — service discovery handled by Aspire

## Auth Flow (How Login Works)

```
1. User submits credentials on Login page
2. Web calls POST /api/auth/login on ApiService
3. ApiService validates (LDAP-first + local fallback)
4. On success: stores single-use token in IMemoryCache, returns token
5. Web navigates to GET /Account/PerformLogin?token=xxx (forceLoad)
6. PerformLogin endpoint calls POST /api/auth/validate-token
7. ApiService returns user claims (userId, roles, email)
8. PerformLogin sets auth cookie on the browser
9. User is redirected to the home page (authenticated)
```

## Development Workflow

### Adding New Features
1. Add DTOs to `AspireWebAppTemplate.Core/Contracts/`
2. Add API endpoints to `AspireWebAppTemplate.ApiService/Controllers/`
3. Add HTTP client methods to `AspireWebAppTemplate.Web/Services/`
4. Create UI pages in `AspireWebAppTemplate.Web/Components/Pages/`

### Database Changes
1. Modify entities in `ApiService/Data/Entities/`
2. Update `ApplicationDbContext.cs`
3. Add migration: `Add-Migration MigrationName -Project AspireWebAppTemplate.ApiService -StartupProject AspireWebAppTemplate.ApiService`
4. Apply: `Update-Database -Project AspireWebAppTemplate.ApiService -StartupProject AspireWebAppTemplate.ApiService`

## License

[Add your license information here]

## Contributing

[Add contribution guidelines here]
