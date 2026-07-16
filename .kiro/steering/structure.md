# Project Structure

## Solution Layout

```
AspireWebAppTemplate/
├── AspireWebAppTemplate.AppHost/         ← Aspire orchestrator (dev entry point)
├── AspireWebAppTemplate.ApiService/      ← Backend REST API
├── AspireWebAppTemplate.Web/             ← Blazor Server frontend
├── AspireWebAppTemplate.UI/              ← Shared Razor Class Library
├── AspireWebAppTemplate.Core/            ← Shared domain (DTOs, enums, interfaces)
├── AspireWebAppTemplate.ServiceDefaults/ ← Aspire defaults (telemetry, health)
├── AspireWebAppTemplate.Tests/           ← All tests (property, unit, integration)
├── docs/                                 ← Feature documentation
└── .kiro/                                ← Specs and steering files
```

## Core Project
```
AspireWebAppTemplate.Core/
├── Application/Abstractions/   ← Shared interfaces (INavigationProvider, etc.)
├── Common/                     ← NavModels, Defaults, shared models
├── Contracts/                  ← DTOs grouped by feature
│   ├── Announcements/          ← AnnouncementDto, CreateAnnouncementRequest, UpdateAnnouncementRequest, AnnouncementQueryParams
│   ├── AuditLog/
│   ├── Auth/
│   ├── Email/                  ← EmailTemplateDto, UpdateEmailTemplateRequest, EmailTemplateQueryParams
│   ├── Notifications/          ← NotificationDto, CreateNotificationRequest, NotificationPushRequest, etc.
│   ├── PagePermissions/
│   ├── Roles/
│   └── Users/
├── Domain/Enums/               ← AuditActionType, AuditEntityType, ThemePreference, NotificationCategory, AnnouncementDisplayType, AnnouncementSeverity, EmailType, EmailTemplateCategory, etc.
├── Extensions/                 ← Extension methods
└── Utilities/                  ← Shared utility classes (SecureConnectionString)
    └── Attributes/             ← Custom validation/metadata attributes (ExportColumnAttribute, OptionalPhoneAttribute)
```

## ApiService Project
```
AspireWebAppTemplate.ApiService/
├── Abstractions/               ← Service interfaces (IAuditLogService, IRoleService, IUserService, IAuthService, etc.)
├── Controllers/                ← Thin REST API controllers (extend BaseController, delegate to services)
├── Data/
│   ├── Entities/               ← EF Core entities (ApplicationUser, ApplicationRole, AuditLogEntry, Announcement, EmailTemplate, etc.)
│   ├── Configurations/         ← IEntityTypeConfiguration<T> classes (one per entity)
│   ├── SeedData/               ← Partial class seed data files
│   │   ├── SeedData.cs                    ← Entry point (orchestrates all seed methods)
│   │   ├── SeedData.Roles.cs              ← Default roles
│   │   ├── SeedData.Users.cs              ← Default admin/user accounts
│   │   ├── SeedData.PagePermissions.cs    ← Default page permission records
│   │   ├── SeedData.EmailTemplates.cs     ← Default email templates (all EmailType values)
│   │   └── SeedData.Announcements.cs      ← Sample announcements
│   └── ApplicationDbContext.cs
├── Extensions/                 ← DI registration extensions (ApplicationServiceExtensions)
├── Services/                   ← Business logic and supporting infrastructure
│   ├── Clients/                ← Typed HttpClients (WebCallbackClient)
│   ├── Handlers/               ← Delegating handlers (InternalApiKeyDelegatingHandler)
│   ├── Infrastructure/         ← Accessors, adapters (CurrentUserAccessor)
│   └── *.cs                    ← Business service implementations (NotificationService, AuthService, EmailService, EmailTemplateService, etc.)
└── Utilities/                  ← AuditChangeHelper, etc.
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
├── Utilities/                  ← DataGridUtils, QueryableDataGridUtils
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
├── Services/                   ← Service-level unit tests
└── Layout/                     ← Layout/component tests
```

## Documentation
```
docs/
├── features/                   ← Completed feature specs (requirements, design, tasks)
│   ├── audit-log/
│   ├── controller-service-refactor/
│   ├── email-smtp-integration/
│   ├── page-access-permissions/
│   ├── role-management/
│   └── ...
├── architecture/               ← Architecture decisions
├── guides/                     ← Developer guides
└── README.md

.kiro/
├── specs/{feature-name}/       ← Active specs (requirements.md, design.md, tasks.md)
└── steering/                   ← AI steering files (this folder)
```
