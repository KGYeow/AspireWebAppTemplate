# Implementation Plan: Resource-Based Authorization

## Overview

This plan implements a resource-based (`Module.Action`) permission model that supersedes the shipped page-permission whitelist, using a **coexistence migration**: the new `Permissions` / `RolePermissions` tables are added alongside the retained (read-only) `PagePermissions` table, and the seed process maps legacy `PagePermission` records into equivalent permission grants. Cleanup of the legacy system is explicitly deferred (Req 10.6).

Work proceeds bottom-up so each step builds on integrated code: data layer → application contracts → service → API authorization infrastructure → controllers → web context/client → web authorization/UI → seed + migration wiring → tests. All code is C# (.NET 10, EF Core 10, ASP.NET Core, Blazor Server, MudBlazor), matching the existing template conventions (traditional constructors, `#region` grouping, XML docs, UTC datetimes).

New permission contracts live in the distinct feature namespace `AspireWebAppTemplate.Application.Features.Permissions` (folder `Application/Features/Permissions/Contracts/`) to avoid collision with the retained `PagePermissions` contracts (Req 8.10).

## Tasks

- [x] 1. Data layer — entities, EF configuration, DbContext
  - [x] 1.1 Create `Permission` and `RolePermission` EF entities
    - Add `Infrastructure/Data/Entities/Permission.cs` (Id, Key, DisplayName, Module, Description) with XML docs.
    - Add `Infrastructure/Data/Entities/RolePermission.cs` (RoleId, PermissionId, navigation to `ApplicationRole` and `Permission`) with XML docs.
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.2 Add EF Core entity configurations for both entities
    - Add `Infrastructure/Data/Configurations/PermissionConfiguration.cs`: table `Permissions`, `Key` max 100 + unique index + required, `DisplayName` max 200 required, `Module` max 50 required, `Description` max 500 nullable. Inline comment on the unique-index rationale.
    - Add `Infrastructure/Data/Configurations/RolePermissionConfiguration.cs`: table `RolePermissions`, composite PK `(RoleId, PermissionId)`, `RoleId` max 450, FK to `ApplicationRoles` and `Permissions` both with cascade delete. Inline comment explaining cascade-delete choice.
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 1.3 Register DbSets and apply configurations in `ApplicationDbContext`
    - Add `DbSet<Permission>` and `DbSet<RolePermission>` and apply the two configurations in `OnModelCreating`.
    - Confirm no changes to existing Identity tables or the `PagePermissions` table (coexistence).
    - _Requirements: 1.1, 1.2, 10.1, 13.6_

  - [x]* 1.4 Write integration tests for EF entity configuration
    - SQLite in-memory: verify `Key` unique constraint, composite PK on `RolePermission`, cascade delete on role/permission removal, and field lengths.
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 2. Application contracts and permission-key validation
  - [x] 2.1 Create permission DTOs in the `Permissions` feature namespace
    - Add under `Application/Features/Permissions/Contracts/`: `PermissionDto`, `PermissionGroupDto`, `UpdateRolePermissionsRequest` (with `PermissionKeys`), `RolePermissionsDto`, `PageModuleMappingDto`.
    - Namespace `AspireWebAppTemplate.Application.Features.Permissions` (folder is organizational only). Do NOT touch the legacy `PagePermissions` contracts.
    - _Requirements: 8.1, 8.2, 8.4, 8.6, 8.9, 8.10_

  - [x] 2.2 Add the `IPermissionService` interface
    - Add `Application/Abstractions/IPermissionService.cs` with grouped query, role-key query, `GetPermissionsForRolesAsync`, `GetMyPermissionsAsync`, `GetPageModuleMappingsAsync`, and `UpdateRolePermissionsAsync`. Full XML docs + `#region` grouping (Query / Write).
    - _Requirements: 5.1, 8.1, 8.2, 8.4, 8.6, 8.9_

  - [x] 2.3 Implement the permission-key format validator
    - Add a static validator (e.g. `PermissionKey` helper in `Application/Features/Permissions/`) enforcing `^[A-Z][a-zA-Z0-9]{0,49}\.[A-Z][a-zA-Z0-9]{0,49}$` and a helper to extract the module segment (substring before the dot).
    - _Requirements: 1.5, 1.6_

  - [x]* 2.4 Write property test for permission key validation
    - **Property 2: Permission key format validation**
    - **Validates: Requirements 1.5, 1.6**
    - `[Property(MaxTest = 100)]`, tag `// Feature: resource-based-authorization, Property 2: Permission key format validation`. Generate valid and invalid strings; accept iff regex matches.

- [x] 3. Permission service implementation
  - [x] 3.1 Implement `PermissionService` query methods
    - Add `Infrastructure/Services/PermissionService.cs` (traditional ctor, `#region` grouping): grouped-by-module query, `GetRolePermissionKeysAsync`, `GetPermissionsForRolesAsync` (single JOIN of `RolePermission` + `Permission`, distinct, case-insensitive), `GetMyPermissionsAsync` (resolve roles → union), `GetPageModuleMappingsAsync` (static page→module mapping from design).
    - Empty/absent role set returns an empty permission set.
    - _Requirements: 5.1, 5.5, 5.6, 6.3, 8.1, 8.2, 8.6, 8.9_

  - [x]* 3.2 Write property test for union resolution
    - **Property 3: Effective permissions equal the union of role grants**
    - **Validates: Requirements 4.1, 4.7, 5.1**
    - `[Property(MaxTest = 100)]`, tag `Property 3: Effective permissions equal the union of role grants`. Random role→permission mappings; assert distinct union.

  - [x] 3.3 Implement `UpdateRolePermissionsAsync` (full replacement + validation)
    - Full-replacement strategy (empty list clears all); validate every key exists (else `ArgumentException` with invalid keys); reject Admin-role modification (`InvalidOperationException`); allow non-Admin system roles; throw `KeyNotFoundException` for unknown role.
    - _Requirements: 3.4, 8.4, 8.5, 8.7, 8.8_

  - [x]* 3.4 Write property test for full replacement strategy
    - **Property 8: Full replacement strategy for role permissions**
    - **Validates: Requirements 8.4**
    - `[Property(MaxTest = 100)]`, tag `Property 8: Full replacement strategy for role permissions`. After PUT with a valid subset, GET returns exactly that set; empty set clears.

  - [x] 3.5 Add audit logging to `UpdateRolePermissionsAsync`
    - Snapshot before/after key lists; call `IAuditLogService.LogAsync` with ActionType=SettingsChanged, EntityType=Role, EntityId=roleId, EntityName=role DisplayName; OldValues/NewValues as camelCase JSON `{ "permissions": [...] }`; identity via `ICurrentUserAccessor`; swallow audit failures (log Error).
    - _Requirements: 11.1, 11.2, 11.3, 11.4_

  - [x]* 3.6 Write property test for audit accuracy
    - **Property 10: Audit entry accurately reflects permission changes**
    - **Validates: Requirements 11.1, 11.2**
    - `[Property(MaxTest = 100)]`, tag `Property 10: Audit entry accurately reflects permission changes`. Random before/after sets; assert Old/New JSON, EntityId, EntityName.

  - [x]* 3.7 Write unit tests for `PermissionService`
    - Moq-based: duplicate assignment rejection, Admin-role immutability, invalid-key rejection, role-not-found 404 path.
    - _Requirements: 1.7, 3.4, 8.5, 8.7_

- [x] 4. API authorization infrastructure
  - [x] 4.1 Implement `PermissionRequirement` and `PermissionPolicyProvider`
    - Add `ApiService/Authorization/PermissionRequirement.cs` (`IAuthorizationRequirement` carrying the required key).
    - Add `ApiService/Authorization/PermissionPolicyProvider.cs` (dynamic `IAuthorizationPolicyProvider` creating a policy for any permission-key string; fall back to default provider for known policies).
    - _Requirements: 4.1_

  - [x] 4.2 Implement `PermissionAuthorizationHandler`
    - Add `ApiService/Authorization/PermissionAuthorizationHandler.cs`: Admin role-name bypass (case-insensitive, no DB query); resolve role IDs from claims; call `PermissionService.GetPermissionsForRolesAsync`; per-request scoped cache; fail closed on empty roles / DB error (log Error); succeed iff required key present.
    - _Requirements: 3.1, 3.5, 4.1, 4.2, 4.7, 4.8, 4.9, 5.2, 5.3, 5.4, 5.6, 5.7, 12.2, 12.3, 12.4_

  - [x]* 4.3 Write property test for Admin bypass (handler)
    - **Property 1: Admin role bypasses all permission checks**
    - **Validates: Requirements 3.1, 3.2, 3.5, 4.2, 6.4, 13.3**
    - `[Property(MaxTest = 100)]`, tag `Property 1: Admin role bypasses all permission checks`. Random keys (incl. nonexistent); Admin always succeeds without DB query.

  - [x]* 4.4 Write unit tests for `PermissionAuthorizationHandler`
    - 403 for missing permission, 401/deny for unauthenticated / no role claims, Admin bypass, fail-closed on DB error, scoped-cache reuse within a request.
    - _Requirements: 4.7, 4.8, 4.9, 5.6, 5.7_

- [x] 5. Controllers — permission policies and Permission_Controller
  - [x] 5.1 Evolve `PagePermissionsController` into `PermissionController`
    - Add `ApiService/Controllers/PermissionController.cs` (thin, extends `BaseController`, delegates to `IPermissionService`): GET all-grouped, GET by-role, PUT update (full replacement), GET `my-permissions`, GET page-module mappings. Apply `[Authorize(Policy = "Permissions.Manage")]` to management/query endpoints; `[Authorize]` (any authenticated) on `my-permissions` and page-module mappings. Map service exceptions via the central handler (no triad try/catch); return 404 for unknown role.
    - _Requirements: 4.6, 8.1, 8.2, 8.3, 8.4, 8.6, 8.9_

  - [x] 5.2 Convert `UsersController` to permission policies
    - Replace `[Authorize]`: GET (GetUsers, GetUser, GetRolesMetadata@`roles-metadata`, LdapLookup) → `Users.Read`; POST (CreateUser, CreateLdapUser@`ldap-create`, SyncLdapUsers@`ldap-sync`) → `Users.Create`; PUT (UpdateUser) → `Users.Update`; DELETE (DeleteUser) → `Users.Delete`; activation/account-mgmt (ActivateUser, DeactivateUser, ResetPassword, SetRoles) → `Users.Activate`.
    - _Requirements: 4.3_

  - [x] 5.3 Convert `RolesController` to permission policies
    - GET (GetRoles, GetRole, GetUsersInRole) → `Roles.Read`; mutations (CreateRole, UpdateRole, DeleteRole, ActivateRole, DeactivateRole, AssignUsersToRole, RemoveUserFromRole) → `Roles.Manage`.
    - _Requirements: 4.4_

  - [x] 5.4 Convert `AuditLogController` to permission policies
    - GET (GetAuditLog, GetAuditLogEntry) → `AuditLog.Read`; ExportAuditLog → `AuditLog.Export`.
    - _Requirements: 4.5_

  - [x] 5.5 Convert `EmailTemplateController` to permission policies
    - GET (GetAll, GetById) + Preview POST → `EmailTemplates.Read`; PUT (Update) → `EmailTemplates.Update`. Do NOT add create/delete policies (no such endpoints).
    - _Requirements: 4.10_

  - [x]* 5.6 Write unit tests for `PermissionController`
    - Endpoint response codes: 200 success, 400 invalid keys / Admin immutability, 403 missing permission, 404 unknown role; `my-permissions` reachable by any authenticated user.
    - _Requirements: 8.3, 8.5, 8.6, 8.7_

- [x] 6. Web project — permission context and API client
  - [x] 6.1 Add `IPermissionContext` and `ApiPermissionService`
    - Add `Web/Abstractions/IPermissionContext.cs` (IsLoaded, IsAdmin, HasPermission, HasAnyPermissionInModule, InitializeAsync).
    - Add `Web/Services/ApiClients/ApiPermissionService.cs` (typed HttpClient, Aspire discovery, returns `ApiResult<T>`, never throws): all-grouped, by-role, update, my-permissions, page-module mappings.
    - _Requirements: 7.1, 8.1, 8.2, 8.4, 8.6, 8.9_

  - [x] 6.2 Implement `PermissionContext` (per-circuit cache)
    - Add `Web/Services/Contexts/PermissionContext.cs`: load effective permissions on `InitializeAsync` via `ApiPermissionService`; cache in a case-insensitive `HashSet<string>`; `IsAdmin` from claims; `HasPermission` (contains key OR Admin, false while not loaded for non-Admin); `HasAnyPermissionInModule` (any key with `module + "."` prefix OR Admin); graceful degradation (log Warning, empty cache, IsLoaded=true) on failure/unauthenticated.
    - _Requirements: 3.2, 6.1, 6.5, 6.7, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 7.8_

  - [x]* 6.3 Write property test for HasPermission correctness
    - **Property 5: HasPermission correctness**
    - **Validates: Requirements 7.2, 7.6**
    - `[Property(MaxTest = 100)]`, tag `Property 5: HasPermission correctness`. Random keys + cached sets; case-insensitive contains OR Admin; false while not loaded for non-Admin.

  - [x]* 6.4 Write property test for module membership
    - **Property 4: Module membership determines page visibility**
    - **Validates: Requirements 6.2, 6.6, 7.3**
    - `[Property(MaxTest = 100)]`, tag `Property 4: Module membership determines page visibility`. `HasAnyPermissionInModule` true iff a key has the module prefix OR Admin.

  - [x]* 6.5 Write unit tests for `PermissionContext`
    - IsLoaded lifecycle, API-failure graceful degradation, unauthenticated skip, Admin short-circuit.
    - _Requirements: 7.5, 7.7, 7.8_

- [x] 7. Web project — page authorization and navigation UI
  - [x] 7.1 Update `PagePermissionHandler` to module-based checks
    - Use `IPermissionContext.HasAnyPermissionInModule(module)` with the page→module mapping; unmapped routes granted (authenticated only); Admin always granted. Replace `PagePermissionContext` usage.
    - _Requirements: 6.6, 6.8, 7.3_

  - [x]* 7.2 Write property test for unmapped-page bypass
    - **Property 7: Unmapped pages bypass module permission checks**
    - **Validates: Requirements 6.8**
    - `[Property(MaxTest = 100)]`, tag `Property 7: Unmapped pages bypass module permission checks`. Random non-admin paths granted with auth only.

  - [x] 7.3 Rename the "Page Permissions" nav item and href
    - In `DefaultNavigationProvider`, rename "Page Permissions" → "Permission Management" and href `admin/page-permissions` → `admin/permission-management`; keep `AuthorizedOnly = true`. Do NOT rewrite stored legacy `PagePermission` paths.
    - _Requirements: 6.4, 10.4, 10.5_

  - [x] 7.4 Point navigation filtering at `PermissionContext`
    - Update `NavMenu` (and layout/circuit init) to initialize and consult `IPermissionContext` for admin-page visibility, replacing `PagePermissionContext` as the visibility authority.
    - _Requirements: 6.1, 6.2, 6.4, 6.5_

  - [x] 7.5 Build the `PermissionManagement.razor` matrix page
    - Add `Web/Components/Pages/Admin/PermissionManagement.razor` at `/admin/permission-management` (Permissions.Manage): roles as columns (active only, Position asc), permissions grouped by module rows; `PageContent` loading wrapper; Admin column checked+disabled with tooltip "Admin always has full access" (no API call); per-toggle auto-save (full replacement) for non-Admin; disable a role's checkboxes during save; success/error Snackbar; revert on failure; error message + no matrix on initial-load failure.
    - _Requirements: 3.3, 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7, 9.8, 9.9, 9.10_

- [x] 8. Seed, migration, and DI wiring
  - [x] 8.1 Add `SeedData.Permissions.cs` (16 permissions, idempotent, Admin grants)
    - New partial `Infrastructure/Data/SeedData/SeedData.Permissions.cs` in a `#region Permissions`: upsert 16 permission definitions by key (no duplicates, existing left unchanged); assign all 16 to Admin; add only-new on future runs; skip + log warning if Admin role absent; log Error and continue on per-record DB failure. Invoke from `SeedData.InitializeAsync`.
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7_

  - [x]* 8.2 Write property test for seed idempotence
    - **Property 6: Seed process is idempotent and non-destructive**
    - **Validates: Requirements 2.2, 2.4, 2.5**
    - `[Property(MaxTest = 100)]`, tag `Property 6: Seed process is idempotent and non-destructive`. Run seed N≥1 times: exactly 16 permissions, Admin has all, pre-existing non-Admin grants unchanged.

  - [x] 8.3 Add legacy PagePermission → permission mapping to the seed
    - In `SeedData.Permissions.cs`, read legacy `PagePermission` records (original paths) and grant: `/admin/user-management`→Users.Read, `/admin/role-management`→Roles.Read, `/admin/audit-log`→AuditLog.Read, `/admin/email-templates`→EmailTemplates.Read, `/admin/page-permissions`→Permissions.Manage; Admin roles get all; skip + log warning on unmapped paths. Read legacy paths independently of the nav rename.
    - _Requirements: 10.2, 10.8_

  - [x]* 8.4 Write property test for migration mapping correctness
    - **Property 9: PagePermission migration mapping correctness**
    - **Validates: Requirements 10.2**
    - `[Property(MaxTest = 100)]`, tag `Property 9: PagePermission migration mapping correctness`. Random PagePermission records → mapped grants per the correspondence table.

  - [x] 8.5 Register services in DI
    - API: register `IPermissionService`→`PermissionService`, `PermissionAuthorizationHandler`, and `PermissionPolicyProvider` in `InfrastructureServiceExtensions` / `Program.cs` (scoped where appropriate).
    - Web: register `ApiPermissionService` in `ApiClientServiceExtensions.AddApiClients`; register `IPermissionContext`→`PermissionContext` (scoped, per-circuit) in `ApplicationServiceExtensions.AddApplicationServices`.
    - _Requirements: 4.1, 5.3, 7.1_

  - [x] 8.6 Create the EF Core migration (retain PagePermissions)
    - Generate a migration adding `Permissions` and `RolePermissions` tables (schema + composite PK + cascade FKs) while leaving `PagePermissions` and Identity tables unmodified.
    - _Requirements: 10.1, 10.7, 13.6_

  - [x]* 8.7 Write integration test for end-to-end seed + migration
    - SQLite in-memory with real DbContext: seed produces 16 permissions + Admin grants + legacy-mapped non-Admin grants; full JOIN resolution query returns the correct union.
    - _Requirements: 2.1, 2.3, 5.5, 10.2_

- [x] 9. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional (tests) and can be skipped for a faster MVP; core implementation tasks are never optional.
- Each task references specific requirement sub-clauses for traceability.
- Property tests use FsCheck.Xunit with `[Property(MaxTest = 100)]` and the tag format `// Feature: resource-based-authorization, Property {N}: {title}`, placed close to the implementation they validate.
- This is a **coexistence migration**: the `PagePermissions` table and its data are retained read-only; the runtime authority becomes `PermissionAuthorizationHandler` / `PermissionContext`. Cleanup of the legacy system is deferred (Req 10.6) and is NOT part of this feature.
- New permission contracts intentionally reuse names within the distinct `Features.Permissions` namespace to avoid collision with the legacy `PagePermissions` contracts (Req 8.10).
- This workflow produces planning artifacts only — no implementation is performed here.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1", "2.3", "4.1"] },
    { "id": 1, "tasks": ["1.2", "2.2", "2.4", "4.4"] },
    { "id": 2, "tasks": ["1.3", "1.4", "3.1"] },
    { "id": 3, "tasks": ["3.2", "3.3", "4.2", "6.1"] },
    { "id": 4, "tasks": ["3.4", "3.5", "4.3", "6.2", "8.1"] },
    { "id": 5, "tasks": ["3.6", "3.7", "5.1", "6.3", "6.4", "6.5", "8.2", "8.3"] },
    { "id": 6, "tasks": ["5.2", "5.3", "5.4", "5.5", "7.1", "8.4", "8.5"] },
    { "id": 7, "tasks": ["5.6", "7.2", "7.3", "7.4", "8.6"] },
    { "id": 8, "tasks": ["7.5", "8.7"] }
  ]
}
```
