# Feature Organization & Template/Business Separation

This document defines how code is organized in AspireWebAppTemplate and in the business
applications built from it. It applies across the whole lifecycle:
**Template <- Copied Business Application <- Growing Enterprise Application.**

## Guiding Principle

Organize each project by the axis that has the most items and changes most often together:

- If a project is dominated by **kinds of things** (few, stable kinds) <- **responsibility-first**.
- If a project is dominated by **features** (many, each changing as a unit) <- **feature-first**.

Consistency across the solution means a **shared vocabulary** (`Template` / `Business` markers
and identical feature names), **not** identical folder trees. Each project uses the structure
that best represents its own responsibility.

## Per-Project Organization

| Project | Top-level axis | Template/Business marker | Feature level |
|---------|----------------|--------------------------|---------------|
| **Domain** | Responsibility (`Enums`, `Constants`, `Attributes`, `Entities`) | In growing folders | Optional |
| **Application** | **Feature-first** under `Features/{Owner}/{Feature}/` | Yes (`Features/Template`, `Features/{Module}`) | **Yes** |
| **Infrastructure** | Responsibility (`Data`, `Services`, `Identity`, ...) | In `Services/` (and `Data` when it grows) | **`Services/` only** |
| **ApiService** | Responsibility (`Controllers`) | Yes (`Controllers/Template`, `Controllers/Business`) | **No** - one controller = one resource; add a `{Module}/` or `{Feature}/` folder only when a module/feature spans multiple controllers |
| **Web** | Responsibility (`Pages`, `Layout`, `Services`) - already area-clustered | Not yet | Reactively, per busy area |
| **UI (shared)** | Responsibility | No | No |

### Application layer (feature-first)

```
Application/
+-- Common/                     <- cross-cutting SHAPES only (ApiResult, PagedResult, NavItem)
+-- Abstractions/               <- ONLY layer-wide contracts (ICurrentUserAccessor, IExcelExportService, ITimeZoneHelper)
+-- Extensions/ , Utilities/    <- pure, dependency-free helpers
+-- Features/
    +-- Template/               <- template-owned features
    |   +-- AuditLog/
    |   |   +-- IAuditLogService.cs      <- behavioral abstraction at the feature root
    |   |   +-- Contracts/               <- data contracts (DTOs, requests, query params, results)
    |   |       +-- AuditLogEntryDto.cs
    |   |       +-- AuditLogQueryParams.cs
    |   |       +-- AuditLogRequest.cs
    |   +-- Users/ , Roles/ , Notifications/ , Announcements/
    |   +-- Email/ , Authentication/ , PagePermissions/ , Ai/
    |   +-- Navigation/          <- interfaces only (no Contracts/ folder: it has no DTOs)
    +-- {BusinessModule}/       <- business-owned features (added by your app)
        +-- {Feature}/
            +-- I{Feature}Service.cs
            +-- Contracts/
```

- Each feature separates **behavioral abstractions from data contracts**:
  interface(s) sit at the feature root; DTOs/requests/results live in a `Contracts/` subfolder.
- A feature with **no DTOs** (e.g. `Navigation`) has no `Contracts/` folder - do not create empty folders.
- **Namespace rule (important):** the `Contracts/` folder is organizational only. Its files keep the
  **feature namespace** (`...Application.Features.{Owner}.{Feature}`), NOT a `.Contracts` namespace.
  This gives uniform folders while consumers still need only **one** `using` per feature.
  (Folder path intentionally does not mirror namespace here - a deliberate, documented exception.)

### Infrastructure layer (responsibility-first, features inside Services)

```
Infrastructure/
+-- Data/                       <- Entities, Configurations, Migrations, SeedData (responsibility-first)
|   +-- Entities/Template/       <- template entities; queried by KIND; NOT feature-nested
|   +-- Configurations/Template/ <- EF configs mirror the Template marker
+-- Identity/ , Clients/ , Handlers/ , Options/ , Extensions/ , Utilities/   <- unchanged
+-- Services/
    +-- Template/{Feature}/     <- e.g. Services/Template/AuditLog/AuditLogService.cs
    +-- {BusinessModule}/{Feature}/   <- business service implementations
    +-- CurrentUserAccessor.cs  <- cross-cutting impls stay at Services/ root
    +-- ExcelExportService.cs
```

Namespace: `...Infrastructure.Services.Template.{Feature}` (or `...Services.{Module}.{Feature}`).

`Data/Entities` and `Data/Configurations` are deliberately **responsibility-first**: an entity is
usually one file, and developers query them by kind. Add a Template/Business split there only when
a module grows large enough that per-module schema review becomes common.

### ApiService layer (responsibility-first; controllers stay flat)

A controller is already a cohesive API resource boundary (one route prefix, one primary service).
Do NOT wrap a single controller in its own feature folder - that adds a directory with one file and
duplicates the controller name. Apply the Template/Business ownership marker only; keep controllers
flat within it.

```
ApiService/
+-- Controllers/
    +-- BaseController.cs               <- cross-cutting base (root)
    +-- WeatherController.cs            <- Aspire sample (root)
    +-- Template/                       <- template-owned controllers (flat)
    |   +-- AuditLogController.cs
    |   +-- UsersController.cs
    |   +-- RolesController.cs
    |   +-- NotificationController.cs
    |   +-- ... (Announcements, Email, Auth, PagePermissions, Ai, Navigation)
    +-- Business/                       <- business-owned controllers (flat while few)
        +-- EmployeeController.cs
        +-- PurchaseOrderController.cs
        +-- Hr/                         <- {Module}/ folder appears only when a module grows many controllers
            +-- LeaveController.cs
            +-- TimesheetController.cs
```

Namespaces: `...ApiService.Controllers.Template` (all template controllers share it),
`...ApiService.Controllers.Business` (or `...Controllers.Business.{Module}` once a module folder exists).

**When a `{Feature}/` folder under Controllers is justified:** only when a single feature/resource
splits into multiple controllers (sub-resources, versioning like `V1/`/`V2/`, or controller + feature-local
API filters/conventions). Below that, keep controllers flat.

## Template vs Business Ownership

**Template-owned** (foundation; updated when you pull template changes):
authentication, authorization, user & role management, audit logging, notifications, email,
announcements, page permissions, navigation, common infrastructure, shared UI.

**Business-owned** (your application):
business entities, rules, services, workflows, DTOs, API endpoints, pages/components.

### Dependency direction (enforced)

```
Business features  --depend on--<-  Template features
Template features  --NEVER------<-  Business features
```

Template code must never reference a business namespace. Business code freely consumes template
services (`IAuditLogService`, `INotificationService`, `ICurrentUserAccessor`, ...). This mirrors
Clean Architecture''s inward-dependency rule, applied to ownership. Enforce it with an architecture test.

## Where does new code go<- (decision procedure)

1. **Which feature is this about<-** Create/locate `Features/{Owner}/{Feature}/` (Application) and
   `Services/{Owner}/{Feature}/` (Infrastructure). Create the folder even for a single file.
2. **Which layer / kind is the type<-** interface + DTO <- Application feature folder; implementation <-
   Infrastructure `Services`; EF entity -> `Data/Entities`; controller -> ApiService `Controllers`;
   page <- Web `Pages`.
3. **Template or business<-** `Features/Template` vs `Features/{Module}` (and same for `Services`).
4. **Genuinely cross-feature AND cross-layer<-** Apply the Common/Utilities rules below |
   and prefer moving it into a feature.

## Common / Utilities rules (anti-junk-drawer)

- **`Common`** = cross-cutting **shapes** with little/no logic (`ApiResult`, `PagedResult`, `NavItem`).
  No injected dependencies allowed.
- **`Abstractions`** = only **layer-wide** contracts that belong to no single feature.
- **`Utilities`** = **pure, stateless, dependency-free** functions used by 2+ features.
- **Extension method** = augments a **type you do not own** with cross-cutting behavior
  (e.g. `IQueryable.ApplySort`).
- **Service** = anything with **dependencies, state, or a capability** <- lives in a feature.
- Used by one feature, or encodes one feature''s rules<- <- **move it into that feature.**

> Rule of thumb: **Calculation <- helper. Capability <- service. Shape <- Common. Rule about a feature <- that feature.**

## When to escalate

- Infrastructure `Services/`: feature organization applies now (it is the growth sink).
- `Data/Entities` & `Configurations`: add a `Feature` level under `Business/` only when a module
  exceeds ~15-20 entities.
- ApiService: controllers stay flat under `Template/` or `Business/`. Introduce a `{Module}/` folder under `Business/` when the app has many controllers, and a `{Feature}/` folder only when one feature/module spans multiple controllers.
- Web: add a feature folder under `Pages/{Area}/` when a feature exceeds ~6-8 co-changing files.
- Any single responsibility folder passing ~25-30 files of one kind signals the feature axis has
  become dominant there.

## Namespace & naming conventions

- Namespaces mirror folders exactly: `{Root}.{Layer}.Features.{Owner}.{Feature}`.
- DTO suffixes by intent: `...Request`, `...QueryParams`, `...Dto`, `...Result`.
- Feature names: PascalCase in folders/namespaces; kebab-case in UI routes.
