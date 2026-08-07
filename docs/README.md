# AspireWebAppTemplate — Project Documentation

> **Enterprise Web Application Template**  
> Built with .NET 10.0 • .NET Aspire • Blazor Server • MudBlazor • Entity Framework Core

---

## Overview

This documentation covers the design, requirements, and implementation of the **AspireWebAppTemplate** — an enterprise web application template featuring a Clean Architecture with separated frontend (Blazor Server) and backend (ASP.NET Core Web API), orchestrated by .NET Aspire.

The template provides a production-ready foundation for internal tools and admin portals, featuring user management, role-based access control, LDAP integration, audit logging, and a modern responsive UI.

---

## Documentation Structure

```
docs/
├── architecture/       System-level technical documentation
├── guides/             Developer onboarding and reference guides
├── features/           Feature specifications (requirements, design, tasks)
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
├── profiles/           Context-specific deployment & branding
└── logs/               Implementation session history
```

---

## Architecture

High-level system documentation and design decisions.

| Document | Description |
|----------|-------------|
| [Overview](./architecture/overview.md) | Solution structure, project responsibilities, key patterns |
| [Technology Stack](./architecture/technology-stack.md) | Frameworks, packages, and versions |
| [Authentication](./architecture/authentication.md) | Identity + LDAP flow, cookie auth, token exchange |
| [Data Layer](./architecture/data-layer.md) | EF Core setup, entities, migrations |
| [Component Patterns](./architecture/component-patterns.md) | MudDataGrid, View/Edit mode, instant-save |

### Key Patterns & Utilities

| Utility | Location | Description |
|---------|----------|-------------|
| `DataGridUtils<T>` | AspireWebAppTemplate.UI/Utilities | In-memory MudDataGrid filtering, sorting, pagination |
| `ExcelExportService` | AspireWebAppTemplate.Infrastructure/Services | Excel/CSV export using EPPlus with `[ExportColumn]` attribute |
| `AuditLogService` | AspireWebAppTemplate.Infrastructure/Services | Audit trail recording with fire-and-forget error handling |
| `BaseController` | AspireWebAppTemplate.ApiService/Controllers | Shared controller base with `CurrentUserId`, `ClientIpAddress` |
| `InternalAuthenticationHandler` | AspireWebAppTemplate.ApiService/Authentication | Service-to-service auth via X-User-* headers |
| `UserIdentityDelegatingHandler` | AspireWebAppTemplate.Web/Services/Handlers | Forwards user identity from Web to API on outbound HTTP calls |
| `ApiResult<T>` | AspireWebAppTemplate.Application/Common | Standard typed result wrapper for all API operations |

---

## Guides

Developer onboarding and day-to-day reference.

| Guide | Description |
|-------|-------------|
| [Getting Started](./guides/getting-started.md) | Setup, prerequisites, first run |
| [Coding Standards](./guides/coding-standards.md) | Naming, patterns, MudBlazor conventions |
| [Testing Strategy](./guides/testing-strategy.md) | xUnit, FsCheck — when to use each |
| [Adding a Feature](./guides/adding-a-feature.md) | Spec workflow: requirements → design → tasks |
| [Adding a Page](./guides/adding-a-page.md) | Blazor page template with MudBlazor |

---

## Feature Specifications

Each feature has requirements, technical design, and implementation tasks.

| Feature | Status | Description |
|---------|--------|-------------|
| [User Management](./features/user-management/) | ✅ Complete | Admin CRUD, LDAP import/sync, bulk actions, data grid |
| [Role Management](./features/role-management/) | ✅ Complete | Role CRUD, user assignment, system role protection |
| [User Profile](./features/user-profile/) | ✅ Complete | View/edit profile, avatar, LDAP restrictions |
| [Settings Page](./features/settings-page/) | ✅ Complete | Time Zone, Date/Time Format, Theme switching, instant-save |
| [Audit Log](./features/audit-log/) | ✅ Complete | Recording service, DataGrid page, Excel export, retention |

---

## Deployment Profiles

This template supports multiple deployment contexts. Each profile documents context-specific branding, infrastructure, and deployment.

| Profile | Context | Key Differences |
|---------|---------|-----------------|
| [Jabil](./profiles/jabil/) | Corporate internal apps | LDAP auth, corporate branding, internal hosting |
| [Personal](./profiles/personal/) | Personal / freelance projects | Local auth only, cloud hosting, custom branding |

See [Profiles README](./profiles/README.md) for details on adding new profiles.

---

## Quick Links

| Resource | Location |
|----------|----------|
| Feature Ideas & Roadmap | [`docs/features/IDEAS.md`](./features/IDEAS.md) |
| Project README | [`README.md`](../README.md) |
| Test Project | `AspireWebAppTemplate.Tests/` |
| DataGridUtils | `AspireWebAppTemplate.UI/Utilities/DataGridUtils.cs` |
| ExcelExportService | `AspireWebAppTemplate.Infrastructure/Services/ExcelExportService.cs` |
| ApiResult | `AspireWebAppTemplate.Application/Common/ApiResult.cs` |
