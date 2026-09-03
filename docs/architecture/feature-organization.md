# Feature Organization & Template/Business Separation

This document defines how code is organized in AspireWebAppTemplate and in the business
applications built from it. It applies across the whole lifecycle:
**Template ? Copied Business Application ? Growing Enterprise Application.**

## Guiding Principle

Organize each project by the axis that has the most items and changes most often together:

- If a project is dominated by **kinds of things** (few, stable kinds) ? **responsibility-first**.
- If a project is dominated by **features** (many, each changing as a unit) ? **feature-first**.

Consistency across the solution means a **shared vocabulary** (`Template` / `Business` markers
and identical feature names), **not** identical folder trees. Each project uses the structure
that best represents its own responsibility.

## Per-Project Organization

| Project | Top-level axis | Template/Business marker | Feature level |
|---------|----------------|--------------------------|---------------|
| **Domain** | Responsibility (`Enums`, `Constants`, `Attributes`, `Entities`) | In growing folders | Optional |
| **Application** | **Feature-first** under `Features/{Owner}/{Feature}/` | Yes (`Features/Template`, `Features/{Module}`) | **Yes** |
| **Infrastructure** | Responsibility (`Data`, `Services`, `Identity`, ...) | In `Services/` (and `Data` when it grows) | **`Services/` only** |
| **ApiService** | Responsibility (`Controllers`) | Optional per growth | Only if a feature has multiple controllers |
| **Web** | Responsibility (`Pages`, `Layout`, `Services`) � already area-clustered | Not yet | Reactively, per busy area |
| **UI (shared)** | Responsibility | No | No |

### Application layer (feature-first)

```
Application/
+-- Common/                     ? cross-cutting SHAPES only (ApiResult, PagedResult, NavItem)
+-- Abstractions/               ? ONLY layer-wide contracts (ICurrentUserAccessor, IExcelExportService, ITimeZoneHelper)
+-- Extensions/ , Utilities/    ? pure, dependency-free helpers
+-- Features/
    +-- Template/               ? template-owned features
    �   +-- AuditLog/           ? IAuditLogService.cs + AuditLog DTOs (ONE namespace)
    �   +-- Users/ , Roles/ , Notifications/ , Announcements/
    �   +-- Email/ , Authentication/ , PagePermissions/ , Ai/ , Navigation/
    +-- {BusinessModule}/       ? business-owned features (added by your app)
        +-- {Feature}/          ? I{Feature}Service.cs + its DTOs
```

- One feature folder holds the **service interface(s) and the DTOs that feature consumes**.
- Interface and DTOs share **one namespace**: `...Application.Features.{Owner}.{Feature}`.
  (We intentionally do NOT use a `.Contracts` sub-namespace � it doubled imports for no benefit.)

### Infrastructure layer (responsibility-first, features inside Services)

```
Infrastructure/
+-- Data/                       ? Entities, Configurations, Migrations, SeedData (responsibility-first)
�   +-- Entities/               ? queried by KIND (migrations, schema) ? NOT feature-nested
+-- Identity/ , Clients/ , Handlers/ , Options/ , Extensions/ , Utilities/   ? unchanged
+-- Services/
    +-- Template/{Feature}/     ? e.g. Services/Template/AuditLog/AuditLogService.cs
    +-- {BusinessModule}/{Feature}/   ? business service implementations
    +-- CurrentUserAccessor.cs  ? cross-cutting impls stay at Services/ root
    +-- ExcelExportService.cs
```

Namespace: `...Infrastructure.Services.Template.{Feature}` (or `...Services.{Module}.{Feature}`).

`Data/Entities` and `Data/Configurations` are deliberately **responsibility-first**: an entity is
usually one file, and developers query them by kind. Add a Template/Business split there only when
a module grows large enough that per-module schema review becomes common.

## Template vs Business Ownership

**Template-owned** (foundation; updated when you pull template changes):
authentication, authorization, user & role management, audit logging, notifications, email,
announcements, page permissions, navigation, common infrastructure, shared UI.

**Business-owned** (your application):
business entities, rules, services, workflows, DTOs, API endpoints, pages/components.

### Dependency direction (enforced)

```
Business features  --depend on--?  Template features
Template features  --NEVER------?  Business features
```

Template code must never reference a business namespace. Business code freely consumes template
services (`IAuditLogService`, `INotificationService`, `ICurrentUserAccessor`, ...). This mirrors
Clean Architecture''s inward-dependency rule, applied to ownership. Enforce it with an architecture test.

## Where does new code go? (decision procedure)

1. **Which feature is this about?** Create/locate `Features/{Owner}/{Feature}/` (Application) and
   `Services/{Owner}/{Feature}/` (Infrastructure). Create the folder even for a single file.
2. **Which layer / kind is the type?** interface + DTO ? Application feature folder; implementation ?
   Infrastructure `Services`; EF entity ? `Data/Entities`; controller ? ApiService `Controllers`;
   page ? Web `Pages`.
3. **Template or business?** `Features/Template` vs `Features/{Module}` (and same for `Services`).
4. **Genuinely cross-feature AND cross-layer?** Apply the Common/Utilities rules below �
   and prefer moving it into a feature.

## Common / Utilities rules (anti-junk-drawer)

- **`Common`** = cross-cutting **shapes** with little/no logic (`ApiResult`, `PagedResult`, `NavItem`).
  No injected dependencies allowed.
- **`Abstractions`** = only **layer-wide** contracts that belong to no single feature.
- **`Utilities`** = **pure, stateless, dependency-free** functions used by 2+ features.
- **Extension method** = augments a **type you do not own** with cross-cutting behavior
  (e.g. `IQueryable.ApplySort`).
- **Service** = anything with **dependencies, state, or a capability** ? lives in a feature.
- Used by one feature, or encodes one feature''s rules? ? **move it into that feature.**

> Rule of thumb: **Calculation ? helper. Capability ? service. Shape ? Common. Rule about a feature ? that feature.**

## When to escalate

- Infrastructure `Services/`: feature organization applies now (it is the growth sink).
- `Data/Entities` & `Configurations`: add a `Feature` level under `Business/` only when a module
  exceeds ~15�20 entities.
- ApiService: add a `Feature/` level when a single feature has 2�3+ controllers.
- Web: add a feature folder under `Pages/{Area}/` when a feature exceeds ~6�8 co-changing files.
- Any single responsibility folder passing ~25�30 files of one kind signals the feature axis has
  become dominant there.

## Namespace & naming conventions

- Namespaces mirror folders exactly: `{Root}.{Layer}.Features.{Owner}.{Feature}`.
- DTO suffixes by intent: `...Request`, `...QueryParams`, `...Dto`, `...Result`.
- Feature names: PascalCase in folders/namespaces; kebab-case in UI routes.