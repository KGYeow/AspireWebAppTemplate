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
├── features/           Feature specifications, split by ownership
│   ├── template/           TEMPLATE-OWNED (inherited; pulled from the template repo)
│   └── business/           BUSINESS-OWNED (your application's specs)
└── profiles/           Context-specific deployment & branding
```

Retained feature specs keep `design.md` (and `requirements.md` for cross-cutting capabilities).
Implementation checklists (`tasks.md`) are not retained. See [features/README.md](./features/README.md).

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
| [Feature Organization](./architecture/feature-organization.md) | Feature-first layout and where new code goes |
| [Scheduler](./architecture/scheduler.md) | Batch/scheduled-job console architecture |
| [Steering Strategy](./architecture/steering-strategy.md) | Template vs business steering ownership |

### Key Patterns & Utilities

| Utility | Location | Description |
|---------|----------|-------------|
| `DataGridHelper<T>` | AspireWebAppTemplate.UI/Utilities | In-memory MudDataGrid filtering, sorting, pagination |
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
| [Scheduler Usage](./guides/scheduler-usage.md) | Running and adding batch jobs |
| [Switching Database Provider](./guides/switching-database-provider.md) | Moving from SQL Server to PostgreSQL |
| [Deployment](./guides/deployment.md) | Deployment overview |

---

## Feature Specifications (template)

Specs that ship with the template. Each keeps a technical `design.md`; cross-cutting capabilities also keep `requirements.md`.

| Feature | Docs | Description |
|---------|------|-------------|
| [Audit Log](./features/template/audit-log/) | requirements + design | Recording service, DataGrid page, Excel export, retention |
| [Page Access Permissions](./features/template/page-access-permissions/) | requirements + design | Role × page whitelist matrix, per-circuit cache, nav filtering |
| [Notification System](./features/template/notification-system/) | requirements + design | In-app notifications, preferences, unread tracking |
| [Email & SMTP Integration](./features/template/email-smtp-integration/) | requirements + design | Database-stored templates, SMTP sending, EmailType resolution |
| [Announcement Banner System](./features/template/announcement-banner-system/) | requirements + design | Multi-surface announcements, severity, scheduling, dismissal |
| [Clean Architecture Migration](./features/template/clean-architecture-migration/) | requirements + design | 4-layer Domain/Application/Infrastructure/host structure |
| [Controller/Service Refactor](./features/template/controller-service-refactor/) | requirements + design | Thin controllers + full service layer |
| [Realtime Notifications](./features/template/realtime-notifications/) | design | SignalR push of unread counts |
| [Notification Push / Deep Link](./features/template/notification-push-deep-link/) | design | API→Web callback, snackbar deep-linking |
| [Notification Snackbar Popup](./features/template/notification-snackbar-popup/) | design | Transient toast content and behavior |
| [Navigation Filtering](./features/template/navigation-filtering/) | design | Permission-based nav menu filtering |
| [User Management](./features/template/user-management/) | design | Admin CRUD, LDAP import/sync, bulk actions |
| [Role Management](./features/template/role-management/) | design | Role CRUD, user assignment, system role protection |
| [User Profile](./features/template/user-profile/) | design | View/edit profile, LDAP restrictions |
| [Settings Page](./features/template/settings-page/) | design | Time zone, date/time format, theme, instant-save |
| [Status Alert](./features/template/status-alert/) | design | Self-hiding success/error alert component |
| [Scheduler Dependency Cleanup](./features/template/scheduler-dependency-cleanup/) | design | Focused Scheduler infrastructure seam |

Your application's feature specs live under [`docs/features/business/`](./features/business/).

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
| Feature Docs Ownership Split | [`docs/features/README.md`](./features/README.md) |
| Project README | [`README.md`](../README.md) |
| Test Project | `AspireWebAppTemplate.Tests/` |
| DataGridHelper | `AspireWebAppTemplate.UI/Utilities/DataGridHelper.cs` |
| ExcelExportService | `AspireWebAppTemplate.Infrastructure/Services/ExcelExportService.cs` |
| ApiResult | `AspireWebAppTemplate.Application/Common/ApiResult.cs` |
