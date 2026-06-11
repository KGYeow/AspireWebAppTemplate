# BlazorWebAppTemplate — Project Documentation

> **Jabil Internal Enterprise Application Template**  
> Built with .NET 10.0 • Blazor Server • MudBlazor • Entity Framework Core

---

## Overview

This documentation covers the design, requirements, and implementation history of the **BlazorWebAppTemplate** — an internal enterprise web application template developed at Jabil. The template provides a production-ready foundation for internal tools and admin portals, featuring user management, role-based access control, LDAP integration, and a modern responsive UI.

For brand guidelines and context-specific configuration, see [Deployment Profiles](./profiles/).

---

## Documentation Structure

```
docs/
├── architecture/       System-level technical documentation
├── guides/             Developer onboarding and reference guides
├── features/           Feature specifications (requirements, design, tasks)
│   ├── audit-log/      Audit trail system (entity, service, QueryableDataGridUtils, UI, export)
│   ├── role-management/
│   ├── settings-page/
│   ├── user-management/
│   └── user-profile/
├── profiles/           Context-specific deployment & branding (Jabil, personal, clients)
└── logs/               Implementation session history
```

---

## Architecture

High-level system documentation and design decisions.

| Document | Description |
|----------|-------------|
| [Overview](./architecture/overview.md) | Solution structure, project responsibilities, key patterns |
| [Technology Stack](./architecture/technology-stack.md) | Frameworks, packages, and versions |
| [Authentication](./architecture/authentication.md) | Identity + LDAP flow, claims, role-based access |
| [Data Layer](./architecture/data-layer.md) | EF Core setup, entities, migrations |
| [Component Patterns](./architecture/component-patterns.md) | MudDataGrid, View/Edit mode, instant-save, PillToggle |
| [ADR-001: Blazor Server](./architecture/decisions/001-blazor-server-mode.md) | Why Blazor Server over WASM |
| [ADR-002: MudBlazor](./architecture/decisions/002-mudblazor-ui-library.md) | UI library selection |
| [ADR-003: Scoped Theme Service](./architecture/decisions/003-scoped-theme-service.md) | Real-time theme switching design |

### Key Patterns & Utilities

| Utility | Location | Description |
|---------|----------|-------------|
| `DataGridUtils<T>` | BlazorWebAppTemplate.UI/Utilities | In-memory MudDataGrid filtering, sorting, pagination |
| `QueryableDataGridUtils<T>` | BlazorWebAppTemplate.UI/Utilities | Database-level MudDataGrid operations via IQueryable → EF Core SQL |
| `ExcelExportService` | BlazorWebAppTemplate/Services | Excel file generation using EPPlus |
| `AuditLogService` | BlazorWebAppTemplate/Services | Audit trail recording with fire-and-forget error handling |

---

## Guides

Developer onboarding and day-to-day reference.

| Guide | Description |
|-------|-------------|
| [Getting Started](./guides/getting-started.md) | Setup, prerequisites, first run |
| [Coding Standards](./guides/coding-standards.md) | Naming, patterns, MudBlazor conventions |
| [Testing Strategy](./guides/testing-strategy.md) | xUnit, FsCheck, bUnit — when to use each |
| [Adding a Feature](./guides/adding-a-feature.md) | Spec workflow: requirements → design → tasks |
| [Adding a Page](./guides/adding-a-page.md) | Blazor page template with MudBlazor |
| [Deployment](./guides/deployment.md) | Build, publish, environment config |

---

## Feature Specifications

Each feature has requirements, technical design, and implementation tasks.

| Feature | Status | Description |
|---------|--------|-------------|
| [User Management](./features/user-management/) | ✅ Complete | Admin CRUD, LDAP import, bulk actions, data grid |
| [Role Management](./features/role-management/) | ✅ Complete | Role CRUD, user assignment, system role protection |
| [User Profile](./features/user-profile/) | ✅ Complete | View/edit profile, avatar, LDAP restrictions, OptionalPhone, fw-bold labels |
| [Settings Page](./features/settings-page/) | ✅ Complete | Time Zone, Locale, Date/Time Format, Theme switching, instant-save, PillToggle |
| [Audit Log](./features/audit-log/) | ✅ Complete | Recording service, QueryableDataGridUtils, MudDataGrid page, CSV export, Excel export, retention, navigation integration |

---

## Deployment Profiles

This template supports multiple deployment contexts. Each profile documents context-specific branding, infrastructure, and deployment.

| Profile | Context | Key Differences |
|---------|---------|-----------------|
| [Jabil](./profiles/jabil/) | Corporate internal apps | LDAP auth, corporate SMTP, Jabil branding, internal IIS |
| [Personal](./profiles/personal/) | Personal / freelance projects | Local auth only, cloud hosting, custom branding, no LDAP |

See [Profiles README](./profiles/README.md) for details on adding new profiles (e.g., for freelance clients).

---

## Implementation Logs

Session-by-session development history: [View Logs](./logs/sessions.md)

---

## Dual-Purpose Template

This template is designed for both **corporate** (Jabil) and **personal** (freelance, side projects) use. The core architecture, features, and guides are context-agnostic. Context-specific concerns (LDAP, branding, hosting) are isolated in [Deployment Profiles](./profiles/).

- **Jabil profile**: LDAP integration, Jabil Blue (`#003B6B`) theme, internal IIS hosting
- **Personal profile**: Local auth only, customizable theme, cloud hosting (Azure, Docker, VPS)
- **Accessibility**: WCAG-compliant color contrast, `aria-label` on interactive elements (both profiles)

---

## Quick Links

| Resource | Location |
|----------|----------|
| Feature Ideas & Roadmap | [`docs/features/IDEAS.md`](./features/IDEAS.md) |
| Brand Guidelines | [`docs/architecture/brand-guidelines.md`](./architecture/brand-guidelines.md) |
| Project README | [`README.md`](../README.md) |
| Active Specs | `.kiro/specs/` |
| Test Project | `BlazorWebAppTemplate.Tests/` |
| QueryableDataGridUtils | `BlazorWebAppTemplate.UI/Utilities/QueryableDataGridUtils.cs` |
| ExcelExportService | `BlazorWebAppTemplate/Services/ExcelExportService.cs` |
