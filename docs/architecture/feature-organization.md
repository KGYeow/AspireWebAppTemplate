# Feature Organization & Template Update Strategy

This document defines how code is organized in AspireWebAppTemplate and in the business
applications created from it, and how a business application receives future improvements
from the template.

## Repository model (the ownership boundary)

The template and each business application live in **separate Git repositories**:

```
AspireWebAppTemplate repo  --clone/fork-->  BusinessApp repo (its own history)
```

**The repository boundary IS the template/business ownership boundary.** The template repo
contains only template code. A business repo starts as a copy of the template and then grows
business code on top. "Did the template give me this, or did we write it?" is answered by
**git history / git blame** and the repo split - NOT by folder names. For that reason the code
is organized purely by **feature/responsibility**, with no in-code `Template/`-vs-`Business/`
folders (see "Why no Template/Business folders" below).

## Guiding Principle

Organize each project by the axis that has the most items and changes most often together:

- If a project is dominated by **kinds of things** (few, stable kinds) -> **responsibility-first**.
- If a project is dominated by **features** (many, each changing as a unit) -> **feature-first**.

Consistency across the solution means **identical feature names across layers**, not identical
folder trees. Each project uses the structure that best represents its own responsibility.

## Per-Project Organization

| Project | Top-level axis | Feature folders? |
|---------|----------------|------------------|
| **Domain** | Responsibility (`Enums`, `Constants`, `Attributes`, `Entities`) | Optional / rarely |
| **Application** | **Feature-first** under `Features/{Feature}/` | **Yes** |
| **Infrastructure** | Responsibility (`Data`, `Services`, `Identity`, ...) | **`Services/` only** |
| **ApiService** | Responsibility (`Controllers`) | No - one controller = one resource |
| **Web** | Responsibility (`Pages`, `Layout`, `Services`) - area-clustered | Reactively, per busy area |
| **UI (shared)** | Responsibility | No |

### Application layer (feature-first)

```
Application/
+-- Common/                     <- cross-cutting SHAPES only (ApiResult, PagedResult, NavItem)
+-- Abstractions/               <- ONLY layer-wide contracts (ICurrentUserAccessor, IExcelExportService, ITimeZoneHelper)
+-- Extensions/ , Utilities/    <- pure, dependency-free helpers
+-- Features/
    +-- AuditLog/
    |   +-- IAuditLogService.cs      <- behavioral abstraction at the feature root
    |   +-- Contracts/               <- data contracts (DTOs, requests, query params, results)
    |       +-- AuditLogEntryDto.cs
    |       +-- AuditLogQueryParams.cs
    |       +-- AuditLogRequest.cs
    +-- Users/ , Roles/ , Notifications/ , Announcements/
    +-- Email/ , Authentication/ , PagePermissions/ , Ai/
    +-- Navigation/              <- interfaces only (no Contracts/ folder: it has no DTOs)
    +-- {YourBusinessFeature}/   <- business features sit alongside, same shape
```

- Each feature separates **behavioral abstractions from data contracts**:
  interface(s) sit at the feature root; DTOs/requests/results live in a `Contracts/` subfolder.
- A feature with **no DTOs** (e.g. `Navigation`) has no `Contracts/` folder - do not create empty folders.
- **Namespace rule:** the `Contracts/` folder is organizational only. Its files keep the
  **feature namespace** (`...Application.Features.{Feature}`), NOT a `.Contracts` namespace.
  This gives uniform folders while consumers need only **one** `using` per feature.
  (Folder path intentionally does not mirror namespace here - a deliberate, documented exception.)

### Infrastructure layer (responsibility-first, features inside Services)

```
Infrastructure/
+-- Data/                       <- Entities, Configurations, Migrations, SeedData (responsibility-first)
|   +-- Entities/               <- EF entities; queried by KIND (migrations/schema); NOT feature-nested
|   +-- Configurations/         <- EF configurations, one per entity
+-- Identity/ , Clients/ , Handlers/ , Options/ , Extensions/ , Utilities/
+-- Services/
    +-- {Feature}/              <- e.g. Services/AuditLog/AuditLogService.cs
    +-- CurrentUserAccessor.cs  <- cross-cutting impls stay at Services/ root
    +-- ExcelExportService.cs
```

Namespace: `...Infrastructure.Services.{Feature}`.

**Parallel with Application:** each `Infrastructure/Services/{Feature}/` folder mirrors the matching
`Application/Features/{Feature}/` folder - same feature name - so the interface and its
implementation sit at the same path in both projects and you can navigate interface -> implementation
directly. Example: `Application/Features/AuditLog/` <-> `Infrastructure/Services/AuditLog/`.
Keep this per-feature folder even when it holds a single service - do NOT flatten it (unlike
Controllers). The parallel with the abstraction layer is the organizing principle, and services are
the layer most likely to grow additional related implementations later.

`Data/Entities` and `Data/Configurations` are deliberately **responsibility-first**: an entity is
usually one file and developers query them by kind (migrations, schema review), so they are not
feature-nested.

### Where does a `*Service` go - Application or Infrastructure?

- The **interface** always goes in `Application/Features/{Feature}/` (behavioral abstraction the
  inner layer owns).
- If the implementation touches **any** infrastructure concern - EF Core / `DbContext`, ASP.NET
  Identity (`UserManager`/`RoleManager`/`SignInManager`), HTTP, SMTP, LDAP, file/Excel, cloud SDKs,
  or `IHttpContextAccessor` - it goes in `Infrastructure/Services/{Feature}/`. This is the default
  and covers essentially every data-driven service.
- Only a **pure-orchestration** service with zero infrastructure dependencies may live beside its
  interface in Application. Rare; when in doubt, choose Infrastructure.

**Why implementations stay in Infrastructure (not Application):** this project is a service-layer
(non-DDD) architecture where EF Core IS the data layer (no repository indirection), so a service's
logic and its data access are the same code. Moving such an implementation into Application would
drag `DbContext`/Identity/EF/SMTP/etc. into the inner layer and break the inward-dependency rule
(and risk a circular Application -> Infrastructure reference). Keeping interfaces in Application and
infrastructure-coupled implementations in Infrastructure is the conventionally-correct Clean
Architecture split for this style, and matches Microsoft's eShopOnWeb reference app.

### ApiService layer (responsibility-first; controllers stay flat)

A controller is already a cohesive API resource boundary (one route prefix, one primary service).
Do NOT wrap a single controller in its own feature folder - that adds a directory with one file and
duplicates the controller name. Keep controllers flat under `Controllers/`.

```
ApiService/
+-- Controllers/
    +-- BaseController.cs               <- cross-cutting base
    +-- WeatherController.cs            <- Aspire sample
    +-- AuditLogController.cs
    +-- UsersController.cs
    +-- ... (Roles, Notifications, Announcements, Email, Auth, PagePermissions, Ai, Navigation)
    +-- {YourBusinessController}.cs     <- business controllers sit alongside
```

Namespace: `...ApiService.Controllers` (all controllers share it).

**When a `{Feature}/` folder under Controllers is justified:** only when a single feature/resource
splits into multiple controllers (sub-resources, versioning like `V1/`/`V2/`, or controller +
feature-local API filters/conventions). Below that, keep controllers flat.

## Why no Template/Business folders

Earlier iterations used `Template/` vs `Business/` folders to mark ownership inside the code. That
was removed because:

- **It is not a Clean Architecture concept.** Clean Architecture governs *layer* boundaries and
  dependency direction, not code ownership. No mainstream .NET reference (eShopOnWeb, Ardalis
  templates) uses an ownership folder.
- **The repository split already provides the boundary.** With template and business in separate
  repos, ownership is answered by git history and the repo itself - the in-code folders duplicated
  information git already tracks.
- **It added cost for no architectural value:** deeper trees, longer namespaces
  (`...Features.Template.X`), more `using` statements, and an extra non-standard rule to teach.

Organize by **feature**; let the **repo boundary + git history** express ownership.

## Bringing template improvements into a business application

Because the two repos are separate, use Git - not a folder convention - to sync improvements.

1. **Start the business repo from a clone/fork of the template** so the two share history. This is
   the single most important step: shared history makes later merges/cherry-picks tractable.
   (If you instead copy files into a fresh `git init`, histories are unrelated and merges are painful.)
2. **Add the template as a read-only remote:**
   ```
   git remote add template <template-repo-url>
   git fetch template
   ```
3. **Pull improvements selectively or in bulk:**
   - Selective: `git cherry-pick <template-commit>` for a specific fix.
   - Bulk: `git merge template/main` to absorb a batch of changes.
4. **Expect conflicts where both sides edited the same file.** This is inherent to any sync
   mechanism, not specific to this template. Keep template files you do not need to customize
   unmodified so template updates apply cleanly; when you must customize template code, expect to
   resolve conflicts on future merges.

**NuGet packaging (not used):** genuinely stable, rarely-edited pieces (e.g. the `UI` component
library, `ServiceDefaults`) *could* later be published as versioned NuGet packages so updates become
version bumps instead of merges. The template does **not** do this today - it relies on Git so all
code stays editable in the business repo. Promote a piece to a package only if a real need emerges.

## Where does new code go? (decision procedure)

1. **Which feature is this about?** Create/locate `Features/{Feature}/` (Application) and
   `Services/{Feature}/` (Infrastructure). Create the folder even for a single file.
2. **Which layer / kind is the type?** interface + DTO -> Application feature folder; implementation
   -> Infrastructure `Services`; EF entity -> `Data/Entities`; controller -> ApiService `Controllers`;
   page -> Web `Pages`.
3. **Genuinely cross-feature AND cross-layer?** Apply the Common/Utilities rules below - and prefer
   moving it into a feature.

## Common / Utilities rules (anti-junk-drawer)

- **`Common`** = cross-cutting **shapes** with little/no logic (`ApiResult`, `PagedResult`, `NavItem`).
  No injected dependencies allowed.
- **`Abstractions`** = only **layer-wide** contracts that belong to no single feature.
- **`Utilities`** = **pure, stateless, dependency-free** functions used by 2+ features.
- **Extension method** = augments a **type you do not own** with cross-cutting behavior
  (e.g. `IQueryable.ApplySort`).
- **Service** = anything with **dependencies, state, or a capability** -> lives in a feature.
- Used by one feature, or encodes one feature's rules? -> **move it into that feature.**

> Rule of thumb: **Calculation -> helper. Capability -> service. Shape -> Common. Rule about a feature -> that feature.**

## When to escalate

- Infrastructure `Services/`: feature organization applies now (it is the growth sink).
- `Data/Entities` & `Configurations`: add a `{Feature}/` level only when a module exceeds ~15-20 entities.
- ApiService: controllers stay flat; introduce a `{Feature}/` folder only when one feature spans multiple controllers.
- Web: add a feature folder under `Pages/{Area}/` when a feature exceeds ~6-8 co-changing files.
- Any single responsibility folder passing ~25-30 files of one kind signals the feature axis has become dominant there.

## Namespace & naming conventions

- Namespaces mirror folders exactly: `{Root}.{Layer}.Features.{Feature}`.
- DTO suffixes by intent: `...Request`, `...QueryParams`, `...Dto`, `...Result`.
- Feature names: PascalCase in folders/namespaces; kebab-case in UI routes.