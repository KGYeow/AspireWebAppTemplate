# AspireWebAppTemplate

A comprehensive .NET Aspire web application template built with .NET 10.0, featuring a Clean Architecture with separated frontend and API backend, modern UI with MudBlazor, authentication (local Identity + LDAP), and full audit logging.

## Overview

This is a full-featured .NET Aspire application template designed to kickstart enterprise-level web applications with a Clean Architecture organized into 4 layers: Domain, Application, Infrastructure, and Host. The frontend (Blazor Server) communicates with the backend (ASP.NET Core Web API) via HTTP, orchestrated by .NET Aspire for service discovery, health checks, and telemetry.

## Architecture

```
Browser ←SignalR→ [Web: Blazor Server + MudBlazor]
                         │
                    HttpClient (Aspire service discovery)
                         │
                         ↓
                  [ApiService: Thin Controllers]
                         │
                    ┌─────┴─────┐
                    ↓           ↓
          [Application]  [Infrastructure: EF Core + Identity + Services]
                    │           │
                    └─────┬─────┘
                          ↓
                      [Domain]
                          ↓
                    [SQL Server]
```

## Project Structure

The solution is organized into nine projects:

### 1. **AspireWebAppTemplate.AppHost** (Aspire Orchestrator)
Defines and orchestrates all services, databases, and their references using .NET Aspire.

### 2. **AspireWebAppTemplate.ServiceDefaults** (Shared Aspire Configuration)
Shared configuration for health checks, telemetry (OpenTelemetry), resilience, and service discovery.

### 3. **AspireWebAppTemplate.Domain** (Domain Layer)
Pure domain primitives with zero external dependencies:
- **Enums/** — All domain enumerations (AuditActionType, AuditEntityType, ThemePreference, NotificationCategory, AnnouncementDisplayType, AnnouncementSeverity, EmailType, EmailTemplateCategory, AuthSource, ExportScope)
- **Constants/** — Shared constants (SystemPageDefaults, DateTimeFormatDefaults, ExportDefaults)
- **Attributes/** — Custom validation/metadata attributes (ExportColumnAttribute, OptionalPhoneAttribute)
- **Entities/** — Pure domain entities without Identity dependencies (EmailTemplate)

### 4. **AspireWebAppTemplate.Application** (Application Layer)
Service interfaces, DTOs, and contracts (depends on Domain only):
- **Features/{Owner}/{Feature}/** - Feature-first: each feature co-locates its service interface(s) and DTOs under one namespace (Features/Template/ for template-owned: AuditLog, Users, Roles, Notifications, Announcements, Email, Authentication, PagePermissions, Ai, Navigation; Features/{BusinessModule}/ for business code)
- **Abstractions/** - ONLY layer-wide cross-cutting contracts (ICurrentUserAccessor, IExcelExportService, ITimeZoneHelper)
- **Common/** — Shared models (ApiResult, NavItem, PagedResult)
- **Contracts/** — DTOs grouped by feature:
  - `Ai/` — AI-related request/response DTOs
  - `Announcements/` — AnnouncementDto, CreateAnnouncementRequest, UpdateAnnouncementRequest, AnnouncementQueryParams
  - `AuditLog/` — AuditLogDto, AuditLogQueryParams, AuditLogRequest
  - `Auth/` — LoginRequest, RegisterResponse, etc.
  - `Email/` — EmailTemplateDto, UpdateEmailTemplateRequest, EmailTemplateQueryParams
  - `Notifications/` — NotificationDto, CreateNotificationRequest, NotificationPushRequest, etc.
  - `PagePermissions/` — PagePermissionDto, UpdatePagePermissionsRequest
  - `Roles/` — RoleDto, CreateRoleRequest, UpdateRoleRequest, RoleQueryParams
  - `Users/` — UserDto, CreateUserRequest, UpdateUserRequest, UserQueryParams
- **Extensions/** — Extension methods (NavigationProviderExtensions, QueryableExtensions)
- **Utilities/** — Pure-logic implementations (DefaultNavigationProvider, TimeZoneHelper)

### 5. **AspireWebAppTemplate.Infrastructure** (Infrastructure Layer)
EF Core, Identity, service implementations, and data access (depends on Application):
- **Data/** — ApplicationDbContext, Configurations, Entities, Migrations, SeedData
- **Identity/** — ASP.NET Core Identity entities (ApplicationUser, ApplicationRole)
- **Services/** — All business service implementations (NotificationService, AuthService, EmailService, etc.)
- **Clients/** — Typed HttpClients (WebCallbackClient)
- **Handlers/** — Delegating handlers (InternalApiKeyDelegatingHandler)
- **Extensions/** — DI registration (InfrastructureServiceExtensions)
- **Options/** — Configuration option classes (LdapSettings)
- **Utilities/** — Helper classes (AuditChangeHelper, CurrentUserAccessor, SecureConnectionString)

### 6. **AspireWebAppTemplate.ApiService** (API Host)
Thin HTTP host layer: Controllers, Authentication, and Program.cs (depends on Application + Infrastructure):
- **Controllers/** — Thin REST API controllers (extend BaseController, delegate to services)
- **Authentication/** — InternalAuthenticationHandler (service-to-service auth)
- **Program.cs** — Composition root (DI, middleware, Identity, EF Core configuration)

### 7. **AspireWebAppTemplate.Web** (Frontend)
Blazor Server frontend (Global InteractiveServer mode):
- **Components/Pages/** — Application pages
  - `Account/Auth/` — Login, Register, ForgotPassword, ResetPassword, etc.
  - `Account/Manage/` — Profile, Email, ChangePassword, 2FA, Passkeys, etc.
  - `Admin/UserManagement/` — Admin user CRUD with bulk operations
  - `Admin/RoleManagement/` — Admin role CRUD
  - `Admin/Announcements/` — Admin announcement CRUD with DataGrid, rich text editor
  - `Admin/AuditLog/` — Searchable audit log with export
  - `Admin/EmailTemplates/` — Email template management (view/edit database-stored templates)
  - `Announcements/` — User-facing announcement list with master-detail layout
  - `Account/Notifications/` — Notification list with master-detail layout
  - `Settings/` — User preferences (theme, timezone, date format)
  - `Example/` — Demo pages (Auth, Counter, Weather)
- **Components/Layout/** — MainLayout, AuthLayout, ManageLayout, NavMenu, Topbar
- **Services/** — HTTP client services, contexts, handlers
- **Extensions/** — DI registration extensions (ApiClientServiceExtensions, ApplicationServiceExtensions)
- **Hubs/** — SignalR hubs (NotificationHub)
- **Endpoints/** — Minimal API endpoints (NotificationCallbackEndpoint)
- **Authorization/** — PagePermissionHandler, requirements

### 8. **AspireWebAppTemplate.UI** (Shared UI Components)
Reusable Blazor components and themes:
- **Components/Shared/** — ConfirmationDialog, PageHeader, ModalDialog, PillToggle, StatusAlert, PageContent, LoadingOverlay
- **Components/DataGrid/** — BoolFilterSelect, EnumFilterSelect, StringFilterSelect
- **Theme/** — DefaultTheme (neutral blue) + JabilTheme (corporate brand)
- **Utilities/** — DataGridHelper<T>, QueryableDataGridHelper<T>

### 9. **AspireWebAppTemplate.Tests** (Test Project)
- **Announcements/** — Property-based tests (FsCheck) for announcement service
- **AuditLog/** — Property-based tests for audit features
- **ControllerServiceRefactor/** — Property + unit tests for service layer
- **Email/** — Property-based tests for email template/service features
- **Notifications/** — Property + unit tests for notification features
- **PagePermissions/** — Property + unit tests for page permissions
- **Services/** — Service-level unit tests
- **Layout/** — Layout/component tests

## Technology Stack

- **Framework**: .NET 10.0, .NET Aspire
- **Frontend**: Blazor Server (Global InteractiveServer)
- **UI Components**: MudBlazor 9.5.0, Radzen.Blazor (HtmlEditor only)
- **Backend**: ASP.NET Core Web API with Controllers
- **Database**: SQL Server with Entity Framework Core 10.0.9
- **Authentication**: Cookie-based (Web) + ASP.NET Core Identity (API)
- **Email**: SMTP with database-stored templates (MailKit)
- **LDAP**: Active Directory integration via System.DirectoryServices.Protocols
- **Excel Export**: EPPlus
- **AI Integration**: AWSSDK.BedrockRuntime (Amazon Bedrock Converse API)
- **HTML Sanitization**: Ganss.Xss.HtmlSanitizer
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

### Announcement System
- Multi-surface announcements: persistent banner, dedicated list page, admin CRUD
- Two display types: Banner (top-of-layout) and Standard (list page only)
- Three severity levels: Info, Warning, Critical — with distinct visual styling
- Scheduling: optional start/expiry dates with timezone-aware date entry
- Per-user banner dismissal stored in the database
- Rich text content editing via Radzen HtmlEditor with server-side HTML sanitization (Ganss.Xss)
- Notification integration: optionally notify all users when announcements go live
- Admin management: DataGrid with built-in filtering, sorting, pagination, bulk delete
- User list page: responsive master-detail layout with infinite scroll and severity filter
- Collaborative administration: all authorized admins manage the shared announcement pool

### Email Templates & SMTP
- SMTP email sending with database-stored templates (all-in-database architecture)
- Unified `EmailType` enum for template resolution — every email maps to exactly one template
- Two categories: System (security emails like password reset, email confirmation — read-only) and Business (welcome, account deactivation — admin-editable)
- `EmailService` implements both custom `IEmailService` and `IEmailSender<ApplicationUser>` for ASP.NET Core Identity integration
- Admin management page at `/admin/email-templates` with DataGrid filtering and edit form
- Placeholder token system for dynamic content (e.g., `{{UserName}}`, `{{ResetLink}}`)
- Aspire parameter-based SMTP credential management

### Settings & Preferences
- Theme (Light/Dark/System)
- Timezone (auto-detect from browser)
- Date/time format
- Instant-save on change

### Architecture Benefits
- **Clean Architecture**: Domain → Application → Infrastructure → Host dependency flow enforces separation of concerns
- **Scalability**: Frontend and backend can scale independently
- **Service Discovery**: Aspire handles inter-service communication
- **Resilience**: Built-in retry and circuit breaker patterns
- **Observability**: OpenTelemetry tracing, metrics, and logging
- **Separation of Concerns**: Frontend has zero database/Identity dependencies

## Optional / Custom Extensions

Features built on top of the template for specific project needs. Not part of the core template, but demonstrate how to extend it:

### AI Integration (Amazon Bedrock)
- Provider-agnostic AI text generation via Amazon Bedrock (Amazon Nova 2 Lite)
- Converse API with cross-region inference profile (`us.amazon.nova-2-lite-v1:0`)
- Three-tier credential resolution: session credentials → basic credentials → IAM role fallback
- Credentials managed via Aspire secret parameters (never committed to source control)
- 60-second timeout with structured error handling and logging
- See `docs/guides/aws-ai-credentials.md` for setup

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

2. **Configure user secrets**
   The project uses .NET user-secrets for local development secrets. Run this once after cloning:
   ```bash
   dotnet user-secrets set "Parameters:InternalApiKey" "k8sF2mP9xQ4wR7vL1nJ6hT3yA0cB5dE" --project AspireWebAppTemplate.AppHost
   ```
   This sets the shared API key used for internal service-to-service communication (API→Web notification callbacks). The value can be any random string — it just needs to exist. Each developer can use a different value since it's only used locally between the two services running on the same machine.

3. **Configure AWS credentials for AI integration** (optional)
   If you want to use the AI feature (Amazon Bedrock), set your AWS credentials:
   ```bash
   dotnet user-secrets set "Parameters:ai-access-key-id" "your-access-key-id" --project AspireWebAppTemplate.AppHost
   dotnet user-secrets set "Parameters:ai-secret-access-key" "your-secret-access-key" --project AspireWebAppTemplate.AppHost
   dotnet user-secrets set "Parameters:ai-session-token" "your-session-token" --project AspireWebAppTemplate.AppHost
   ```
   Get these values from the AWS console (Option 3: "Use individual values in your AWS service client"). Session tokens expire — see `docs/guides/aws-ai-credentials.md` for details.

4. **Configure the database connection**
   Update `AspireWebAppTemplate.ApiService/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=AspireWebAppTemplateDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
     }
   }
   ```

5. **Run with Aspire (recommended)**
   ```bash
   dotnet run --project AspireWebAppTemplate.AppHost
   ```
   Aspire dashboard opens at `https://localhost:17024` with links to both services.

6. **Or run ApiService standalone** (for EF migrations)
   ```bash
   dotnet run --project AspireWebAppTemplate.ApiService
   ```

7. **Database migrations**
   - Auto-migrates on startup in Development mode
   - Manual: `Update-Database -Project AspireWebAppTemplate.Infrastructure -StartupProject AspireWebAppTemplate.ApiService`
   - CLI: `dotnet ef database update --project AspireWebAppTemplate.Infrastructure --startup-project AspireWebAppTemplate.ApiService`

8. **Default accounts** (seeded automatically)
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
1. Add DTOs to `AspireWebAppTemplate.Application/Contracts/`
2. Add service interface to `AspireWebAppTemplate.Application/Abstractions/`
3. Add service implementation to `AspireWebAppTemplate.Infrastructure/Services/`
4. Add API endpoints to `AspireWebAppTemplate.ApiService/Controllers/`
5. Add HTTP client methods to `AspireWebAppTemplate.Web/Services/`
6. Create UI pages in `AspireWebAppTemplate.Web/Components/Pages/`

### Database Changes
1. Modify entities in `Infrastructure/Data/Entities/` or `Infrastructure/Identity/` or `Domain/Entities/`
2. Update `ApplicationDbContext.cs`
3. Add migration: `Add-Migration MigrationName -Project AspireWebAppTemplate.Infrastructure -StartupProject AspireWebAppTemplate.ApiService`
4. Apply: `Update-Database -Project AspireWebAppTemplate.Infrastructure -StartupProject AspireWebAppTemplate.ApiService`

## License

[Add your license information here]

## Contributing

[Add contribution guidelines here]
