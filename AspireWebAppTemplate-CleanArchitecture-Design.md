# AspireWebAppTemplate — Clean Architecture Design (Four-Project)

**Purpose:** Define a four-project Clean Architecture structure for `AspireWebAppTemplate` — `Domain`, `Application`, `Infrastructure`, `ApiService` — with compiler-enforced dependency direction, while retaining the copy-and-rename distribution workflow and a clear separation between template-owned and business-owned code.

---

## 1. Project Structure

```
AspireWebAppTemplate.AppHost/            → composition root for the distributed system
Enterprise.AspireWebAppPlatform.ServiceDefaults/    → cross-cutting infra defaults; fixed identity, never
                                                          renamed on copy (see §7)

AspireWebAppTemplate.Domain/
├── Entities/
│   ├── Platform/{Feature}/                ← entities with no relationship, direct or by foreign key,
│   │                                            to Identity (see §3 for the placement rule)
│   └── Business/{Feature}/
└── Enums/
    ├── Platform/
    └── Business/
    (Zero dependencies. No reference to Application, Infrastructure, or any framework package.)

AspireWebAppTemplate.Application/
├── Abstractions/
│   ├── Platform/{Feature}/                ← IUserService, IAuditLogService, IIdentityService,
│   │                                            INotificationService, INavigationProvider, etc.
│   └── Business/{Feature}/
├── Contracts/
│   ├── Platform/{Feature}/                 ← DTOs
│   └── Business/{Feature}/
└── Common/
    ├── Platform/
    └── Business/
    (Depends only on Domain.)

AspireWebAppTemplate.Infrastructure/
├── Identity/                                ← ApplicationUser, ApplicationRole, IdentityService, LDAP sync
│                                                 — not split by ownership (§6)
├── Data/
│   ├── ApplicationDbContext.cs                — single file, composes configurations from every location
│   ├── SeedData.cs                             — single file
│   ├── Entities/
│   │   ├── Platform/{Feature}/                    ← AuditLogEntry, Notification, Announcement, PagePermission,
│   │   │                                               and any other entity referencing Identity by foreign key
│   │   │                                               (see §3) — uses ordinary EF navigation properties to
│   │   │                                               ApplicationUser/ApplicationRole, since both are visible here
│   │   └── Business/{Feature}/
│   └── Configurations/
│       ├── Platform/{Feature}/                    ← IEntityTypeConfiguration<T> for the above, and for
│       │                                               Domain's entities
│       └── Business/{Feature}/
├── Services/
│   ├── Platform/{Feature}/                          ← implementations of Application/Abstractions interfaces
│   └── Business/{Feature}/
│       ├── Clients/
│       └── Handlers/
└── Utilities/                                          ← AuditChangeHelper and similar EF-adjacent helpers
    (Depends on Application, and transitively on Domain.)

AspireWebAppTemplate.ApiService/
├── Controllers/
│   ├── Platform/                                      ← UserManagementController, RoleController,
│   │                                                        AuditLogController, NotificationController, etc.
│   └── Business/
└── Program.cs                                           ← calls AddApplicationServices(), wires Infrastructure
    (Depends on Application and Infrastructure — the composition root.)

AspireWebAppTemplate.Web/                  → depends on Application only (Contracts/DTOs); never Domain,
                                               Application/Abstractions, or Infrastructure. No Platform/Business
                                               split (§6). Renamed on copy, same as Domain/Application/
                                               Infrastructure/ApiService.
Enterprise.AspireWebAppPlatform.UI/         → no Platform/Business split (§6). Fixed identity, never renamed
                                               on copy (§7).
AspireWebAppTemplate.Tests/                   → references Domain, Application, and Infrastructure.
```

---

## 2. Dependency Direction and Enforcement

```
ApiService     → Application (Abstractions + Contracts) + Infrastructure (composition)
Infrastructure → Application (implements its interfaces) → Domain (transitively)
Application    → Domain only
Web            → Application (Contracts only)
Domain         → nothing
Tests          → Domain + Application + Infrastructure
```

The compiler enforces most of this directly: a `ProjectReference` that doesn't exist cannot be used. One gap remains structurally possible and is closed explicitly: because `Application` references `Domain`, and project references are transitive, `Domain`'s types are technically visible to any project that references `Application` — including `Web`, which should never see `Domain` directly.

This is closed with `InternalsVisibleTo`:

```xml
<!-- Domain.csproj -->
<ItemGroup>
  <InternalsVisibleTo Include="AspireWebAppTemplate.Application" />
  <InternalsVisibleTo Include="AspireWebAppTemplate.Infrastructure" />
  <InternalsVisibleTo Include="AspireWebAppTemplate.Tests" />
</ItemGroup>
```

```csharp
// Domain/Enums/Platform/ThemePreference.cs
internal enum ThemePreference { /* ... */ }   // internal, not public — same rule applies to any Domain type,
                                                // whether an identity-free entity or an enum
```

`Web` — deliberately excluded from the `InternalsVisibleTo` list — receives a compiler error if any code attempts to reference a `Domain` type directly, even though the type is present transitively on its dependency graph. This closes the one enforcement gap that project separation alone does not.

---

## 3. Entity Placement

**Placement rule:** an entity belongs in `Domain/Entities` only if it has no relationship — inheritance or foreign key — to Identity. An entity belongs in `Infrastructure/Data/Entities` if it references a user or role by foreign key, even without inheriting an Identity base class. `Infrastructure/Identity` remains reserved specifically for types that inherit an Identity base class (`IdentityUser<TKey>`, `IdentityRole<TKey>`, or a customized `IdentityUserClaim`/`IdentityUserRole`) plus the services built directly on top of `UserManager`/`RoleManager`.

In practice, most platform entities (`AuditLogEntry`, `Notification`, `Announcement`, `PagePermission`) carry an audit or ownership relationship to a user or role, and so belong in `Infrastructure/Data/Entities` rather than `Domain/Entities`. `Domain/Entities` is reserved for entities with no identity relationship at all — genuinely infrastructure-agnostic business concepts — which may be a smaller category in practice than `Infrastructure/Data/Entities`, and that is an expected consequence of this rule rather than a sign it's being applied incorrectly.

Because `Infrastructure` already references `Identity` directly, entities placed in `Infrastructure/Data/Entities` use ordinary EF navigation properties and `.Include()` where related data is needed, without the scalar-FK-plus-manual-join pattern that `Domain`-placed entities require:

```csharp
// Infrastructure/Data/Entities/Platform/PagePermission.cs
public class PagePermission
{
    public Guid Id { get; set; }
    public string PagePath { get; set; }
    public Guid RoleId { get; set; }
    public ApplicationRole Role { get; set; }   // ordinary navigation property — both types are visible here
}
```

```csharp
// Infrastructure/Data/Configurations/Platform/PagePermissionConfiguration.cs
public class PagePermissionConfiguration : IEntityTypeConfiguration<PagePermission>
{
    public void Configure(EntityTypeBuilder<PagePermission> builder)
    {
        builder.HasOne(p => p.Role)
               .WithMany()
               .HasForeignKey(p => p.RoleId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
```

For the rarer case of a `Domain`-placed entity needing a foreign key to `ApplicationUser`/`ApplicationRole` — an entity with no other identity relationship, where introducing one purely for a single field isn't worth relocating the whole entity — the same scalar-FK, no-navigation-property pattern applies: a plain `Guid`/`string` field on the `Domain` entity, with the relationship configured via `builder.HasOne<ApplicationRole>().WithMany().HasForeignKey(...)` in `Infrastructure/Data/Configurations`, since `Domain.csproj` has no reference to `Infrastructure.csproj` and cannot declare the navigation property directly.

`ApplicationDbContext` and `SeedData` remain single files regardless of how many entity categories or projects feed into them; `OnModelCreating` composes configurations from `Data/Configurations` and `Identity/Configurations` via `ApplyConfigurationsFromAssembly` or explicit `ApplyConfiguration()` calls.

---

## 4. Identity

`Infrastructure/Identity` is reserved for types that inherit an Identity base class and the services built directly on `UserManager`/`RoleManager`. Entities that merely reference `ApplicationUser`/`ApplicationRole` by foreign key live in `Infrastructure/Data/Entities` (§3), not here — `Identity` stays scoped to the Identity framework's own types, not every entity with an identity relationship.

`Application` and `Domain` reference only an interface, never `ApplicationUser`/`ApplicationRole` directly:

```
Application/Abstractions/Platform/
└── IIdentityService.cs             ← identity operations in plain types only: string userId/userName,
                                          Result objects, UserDto — never ApplicationUser

Infrastructure/Identity/
├── ApplicationUser.cs                ← : IdentityUser<Guid>
├── ApplicationRole.cs                 ← : IdentityRole<Guid>
├── IdentityService.cs                   ← implements IIdentityService; wraps UserManager<ApplicationUser>
│                                            and RoleManager<ApplicationRole> internally
├── LdapSyncService.cs
└── Configurations/
    └── ApplicationUserConfiguration.cs   ← EF configuration beyond IdentityDbContext's defaults
```

`ApplicationDbContext` extends `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`. `Application` and `ApiService/Controllers` reference `IIdentityService` and DTOs only.

---

## 5. Template-Owned and Business-Owned Code

The `Platform`/`Business` split applies within `Domain`, `Application`, `Infrastructure`, and `ApiService`. `Web` and `UI` carry no internal split: `UI` holds no business content by design, and `Web`'s business additions go directly into its existing `Components`/`Pages` structure using the feature-folder convention already governing that project.

Architectural layer is the primary folder axis; ownership (`Platform`/`Business`) is nested within each layer, as shown in §1. A recursive match on folders named `Platform` (`**/Platform/**`) locates every template-owned folder for update purposes regardless of which layer it sits under.

`ApplicationDbContext.cs`, `SeedData.cs`, and `Infrastructure/Identity` are not split by ownership: an application has one database context, and business applications consume identity through `IIdentityService` rather than adding their own Identity infrastructure.

---

## 6. Template Update Strategy

A template update replaces the contents of every folder named `Platform`, across `Domain`, `Application`, `Infrastructure`, and `ApiService`, with the new release's version, then reapplies any documented extension-point configuration. Business code, in disjoint `Business`-named folders, is untouched, since the paths never overlap.

**Extension points:**

1. **Dependency Injection substitution** — the default mechanism. A business application registers its own implementation of a `Platform` interface after the platform registration.
2. **Partial classes**, within a project boundary only. `ApplicationUser` in `Infrastructure/Identity` can be extended with a `partial class` defined under `Infrastructure/Identity` (business-owned file) or `Infrastructure/Data/Entities/Business`, provided the extension lives in the same project as the type being extended — this no longer works across the `Domain`/`Infrastructure` boundary the way it did under a single-project design, only within each project individually. Since most platform entities now live in `Infrastructure/Data/Entities` rather than `Domain` (§3), this extension pattern applies to them without needing to cross a project boundary at all.
3. **Virtual methods** on Platform services, used narrowly, with DI substitution as the default.
4. **Domain events** for business code reacting to Platform lifecycle events, via an interface declared in `Application/Abstractions`.
5. **Configuration-based toggles** for whole-feature enable/disable, extending the pattern already used for optional AI integration.

**Update procedure:** compare the incoming release's `Platform`-named folders against the business application's current ones (`diff -r`, or a Git merge scoped to `**/Platform/**`), replace them, rebuild. Only extension-point files — deliberately located outside `Platform` folders — are candidates for a follow-up change.

---

## 7. Project Rename Exemptions

Every project except `UI` and `ServiceDefaults` is renamed as part of copy-and-rename. `UI` and `ServiceDefaults` hold no business-owned content by design and are wholesale-replaceable on a template update; both carry a fixed identity, applied consistently across namespace, `AssemblyName`, `.csproj` filename, folder name, and `.sln` entry, set once and never changed on any copy:

```
Enterprise.AspireWebAppPlatform.UI
Enterprise.AspireWebAppPlatform.ServiceDefaults
```

---

## 8. Naming Conventions

| Element | Convention | Notes |
|---|---|---|
| **Projects** | `AppHost`, `Domain`, `Application`, `Infrastructure`, `ApiService`, `Web`, `Tests` renamed on copy; `UI`, `ServiceDefaults` fixed as `Enterprise.AspireWebAppPlatform.*` (§7). |
| **Entities (identity-free)** | `Domain/Entities/{Platform\|Business}/{Feature}/`, no suffix (`Notification`, not `NotificationEntity`) | Reserved for entities with no relationship to Identity (§3). |
| **Entities (identity-referencing)** | `Infrastructure/Data/Entities/{Platform\|Business}/{Feature}/`, same naming, no suffix | Entities referencing `ApplicationUser`/`ApplicationRole` by foreign key, using ordinary navigation properties (§3). Expected to hold most platform entities in practice. |
| **Identity-coupled types** | `Infrastructure/Identity/`, unsplit — `ApplicationUser`, `ApplicationRole`, `IdentityService`, `IIdentityService` | Reserved for Identity-framework-inheriting types only, not every entity with an identity relationship (§4). |
| **Service interfaces** | `Application/Abstractions/{Platform\|Business}/{Feature}/`, `I`-prefixed. |
| **DTOs** | `Application/Contracts/{Platform\|Business}/{Feature}/`, suffixed `Dto`/`Request`/`Response`. |
| **EF Core configurations** | `Infrastructure/Data/Configurations/{Platform\|Business}/{Feature}/`, suffixed `Configuration`. |
| **Service implementations** | `Infrastructure/Services/{Platform\|Business}/{Feature}/`, suffixed `Service`, implementing an `I{Name}Service` from `Application`. |
| **Controllers** | `ApiService/Controllers/{Platform\|Business}/`, suffixed `Controller`, extending `BaseController`. |

---

## 9. Priority Roadmap

1. Establish the four projects (`Domain`, `Application`, `Infrastructure`, `ApiService`) and their dependency references.
2. Move entities into `Domain`, interfaces and DTOs into `Application`, EF configuration and service implementations into `Infrastructure`, per §1 and §3.
3. Apply the `InternalsVisibleTo` restriction on `Domain` (§2).
4. Establish `Infrastructure/Identity` and `IIdentityService` (§4).
5. Apply the `Platform`/`Business` split within each of the four projects (§5).
6. Set the fixed identity for `UI` and `ServiceDefaults` (§7).
7. Add the update tooling and extension-point documentation (§6).
