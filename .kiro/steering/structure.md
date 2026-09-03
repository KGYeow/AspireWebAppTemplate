# Project Structure

## Solution Layout

```
AspireWebAppTemplate/
├── AspireWebAppTemplate.AppHost/         ← Aspire orchestrator (dev entry point)
├── AspireWebAppTemplate.Domain/          ← Domain layer (enums, constants, attributes, pure entities)
├── AspireWebAppTemplate.Application/     ← Application layer (interfaces, DTOs, contracts, extensions)
├── AspireWebAppTemplate.Infrastructure/  ← Infrastructure layer (EF Core, Identity, services, data access)
├── AspireWebAppTemplate.ApiService/      ← API host (thin controllers, authentication, Program.cs)
├── AspireWebAppTemplate.Web/             ← Blazor Server frontend
├── AspireWebAppTemplate.UI/              ← Shared Razor Class Library
├── AspireWebAppTemplate.ServiceDefaults/ ← Aspire defaults (telemetry, health)
├── AspireWebAppTemplate.Tests/           ← All tests (property, unit, integration)
├── docs/                                 ← Feature documentation
└── .kiro/                                ← Specs and steering files
```

## Domain Project
```
AspireWebAppTemplate.Domain/
├── Attributes/                 ← Custom validation/metadata attributes (ExportColumnAttribute, OptionalPhoneAttribute)
├── Constants/                  ← Shared constants (SystemPageDefaults, DateTimeFormatDefaults, ExportDefaults)
├── Entities/                   ← Pure domain entities without Identity dependencies (EmailTemplate)
├── Enums/                      ← All domain enumerations (AuditActionType, AuditEntityType, ThemePreference, NotificationCategory, AnnouncementDisplayType, AnnouncementSeverity, EmailType, EmailTemplateCategory, AuthSource, ExportScope)
└── ValueObjects/               ← Value objects (reserved for future use)
```

## Application Project (feature-first)

Organized by feature under Features/{Owner}/{Feature}/. Each feature folder holds its service
interface(s) AND its DTOs under a single per-feature namespace
(AspireWebAppTemplate.Application.Features.{Owner}.{Feature}).

```
AspireWebAppTemplate.Application/
├── Common/                     <- Cross-cutting SHAPES only (ApiResult, NavItem, PagedResult)
├── Abstractions/               <- ONLY layer-wide contracts (ICurrentUserAccessor, IExcelExportService, ITimeZoneHelper)
├── Extensions/                 <- Extension methods (NavigationProviderExtensions, QueryableExtensions)
├── Utilities/                  <- Pure-logic implementations (DefaultNavigationProvider, TimeZoneHelper)
└── Features/
    ├── Template/               <- template-owned features (interface + DTOs per feature, one namespace)
    │   ├── AuditLog/           <- IAuditLogService + AuditLog DTOs
    │   ├── Users/  Roles/  Notifications/  Announcements/
    │   ├── Email/  Authentication/  PagePermissions/  Ai/  Navigation/
    └── {BusinessModule}/       <- business-owned features (e.g., Hr/EmployeeManagement)
```

See docs/architecture/feature-organization.md for the full convention.
## Infrastructure Project
```
AspireWebAppTemplate.Infrastructure/
├── Clients/                    ← Typed HttpClients (WebCallbackClient)
├── Data/
│   ├── ApplicationDbContext.cs ← EF Core DbContext
│   ├── Configurations/         ← IEntityTypeConfiguration<T> classes (one per entity)
│   ├── Entities/              <- EF Core entities (responsibility-first: queried by kind, NOT feature-nested)
│   ├── Migrations/             ← EF Core migration files
│   └── SeedData/               ← Partial class seed data files
│       ├── SeedData.cs                    ← Entry point (orchestrates all seed methods)
│       ├── SeedData.Roles.cs              ← Default roles
│       ├── SeedData.Users.cs              ← Default admin/user accounts
│       ├── SeedData.PagePermissions.cs    ← Default page permission records
│       ├── SeedData.EmailTemplates.cs     ← Default email templates (all EmailType values)
│       └── SeedData.Announcements.cs      ← Sample announcements
├── Extensions/                 ← DI registration (InfrastructureServiceExtensions)
├── Handlers/                   ← Delegating handlers (InternalApiKeyDelegatingHandler)
├── Identity/                   ← ASP.NET Core Identity entities (ApplicationUser, ApplicationRole)
├── Options/                    ← Configuration option classes (LdapSettings)
├── Services/                <- Feature-first impls: Template/{Feature}/ (e.g. Template/AuditLog/AuditLogService.cs); business under {Module}/; cross-cutting (CurrentUserAccessor, ExcelExportService) at Services/ root
└── Utilities/                  ← Helper classes (AuditChangeHelper, CurrentUserAccessor, SecureConnectionString)
```

## ApiService Project
```
AspireWebAppTemplate.ApiService/
├── Authentication/             ← InternalAuthenticationHandler (service-to-service auth)
├── Controllers/                ← Thin REST API controllers (extend BaseController, delegate to services)
└── Program.cs                  ← Composition root (DI, middleware, Identity, EF Core configuration)
```

## Web Project
```
AspireWebAppTemplate.Web/
├── Authentication/             ← Internal API key auth handler (service-to-service)
├── Common/
│   └── Defaults/               ← Centralized constants (AssetDefaults — logo/background paths)
├── Components/
│   ├── Layout/                 ← Region-based layout organization
│   │   ├── MainLayout.razor    ← Entry-point layouts at root level
│   │   ├── AuthLayout.razor
│   │   ├── ManageLayout.razor
│   │   ├── Topbar/             ← Topbar, DropdownProfile, NotificationBell, AnnouncementIcon, AnnouncementBanner
│   │   ├── Sidebar/            ← DrawerHeader, NavMenu, ManageNavMenu
│   │   ├── Footer/             ← Footer
│   │   └── Shared/             ← ReconnectModal, etc.
│   ├── Pages/
│   │   ├── Account/            ← Profile, Settings, Auth, Manage, Notifications
│   │   ├── Admin/              ← AuditLog, UserManagement, RoleManagement, PagePermissions, Announcements, EmailTemplates
│   │   ├── Announcements/      ← User-facing announcement list page (master-detail)
│   │   └── Example/            ← Counter, Weather, Auth demo
│   └── Shared/                 ← Web-specific shared components
├── Endpoints/                  ← Minimal API endpoints (NotificationCallbackEndpoint)
├── Extensions/                 ← DI registration extensions (ApiClientServiceExtensions, ApplicationServiceExtensions)
├── Hubs/                       ← SignalR hubs (NotificationHub)
├── Services/
│   ├── ApiClients/             ← Typed HttpClient services (ApiUserService, ApiNotificationService, ApiAnnouncementService, etc.)
│   ├── Contexts/               ← Per-circuit scoped state (NotificationContext, AnnouncementContext, CircuitUserContext)
│   ├── Handlers/               ← Delegating handlers (UserIdentityDelegatingHandler)
│   └── ExponentialBackoffRetryPolicy.cs
├── Authorization/              ← PagePermissionHandler, requirements
└── wwwroot/                    ← Static assets (css, js, images)
```

## UI Project (Razor Class Library)
```
AspireWebAppTemplate.UI/
├── Components/Shared/          ← Reusable components (PageContent, LoadingOverlay, PageHeader, StatusAlert, PillToggle, ModalDialog, etc.)
├── Components/DataGrid/        ← DataGrid filter components (BoolFilterSelect, EnumFilterSelect, StringFilterSelect)
├── Utilities/                  ← DataGridHelper, QueryableDataGridHelper
└── Theme/                      ← DefaultTheme (neutral blue) + JabilTheme (corporate brand)
```

## Tests Project
```
AspireWebAppTemplate.Tests/
├── Announcements/              ← Property + unit tests for announcement features
├── ControllerServiceRefactor/  ← Property + unit tests for service layer
├── AuditLog/                   ← Property + unit tests for audit features
├── Email/                      ← Property + unit tests for email template/service features
├── Notifications/              ← Property + unit tests for notification features
├── PagePermissions/            ← Property + unit tests for page permissions
├── Services/                <- Feature-first impls: Template/{Feature}/ (e.g. Template/AuditLog/AuditLogService.cs); business under {Module}/; cross-cutting (CurrentUserAccessor, ExcelExportService) at Services/ root
└── Layout/                     ← Layout/component tests
```

## Documentation
```
docs/
├── features/                   ← Completed feature specs (requirements, design, tasks)
│   ├── announcement-banner-system/
│   ├── audit-log/
│   ├── aws-ai-integration/
│   ├── clean-architecture-migration/
│   ├── email-smtp-integration/
│   ├── navigation-filtering/
│   ├── notification-push-deep-link/
│   ├── notification-snackbar-popup/
│   ├── notification-system/
│   ├── page-access-permissions/
│   ├── realtime-notifications/
│   ├── role-management/
│   ├── settings-page/
│   ├── status-alert/
│   ├── user-management/
│   └── user-profile/
├── architecture/               ← Architecture decisions
├── guides/                     ← Developer guides
└── README.md

.kiro/
├── specs/{feature-name}/       ← Active specs (requirements.md, design.md, tasks.md)
└── steering/                   ← AI steering files (this folder)
```
