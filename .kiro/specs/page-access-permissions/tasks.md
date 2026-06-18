# Implementation Plan: Page Access Permissions

## Overview

Replace hardcoded `[Authorize(Roles = "...")]` attributes with a database-driven, role-based page authorization system. Implementation progresses from shared DTOs and entity modeling, through API service layer, into the Web project's authorization infrastructure, and concludes with the admin UI and cleanup of legacy role attributes.

## Tasks

- [x] 1. Create shared DTOs and PagePermission entity
  - [x] 1.1 Create page permission DTOs in AspireWebAppTemplate.Core
    - Create `Contracts/PagePermissions/` directory
    - Add `RolePermissionsDto` record: `(string RoleId, string RoleName, List<PagePermissionDto> Pages)`
    - Add `PagePermissionDto` record: `(string PagePath, string PageDisplayName)`
    - Add `UpdateRolePermissionsRequest` record: `(List<string> PagePaths)`
    - Include full XML documentation comments on all types and properties
    - _Requirements: 3.1, 3.2, 14.1_

  - [x] 1.2 Create PagePermission entity in ApiService
    - Create `Data/Entities/PagePermission.cs` with Id, RoleId, PagePath, PageDisplayName properties
    - Add navigation property `ApplicationRole Role`
    - Add XML documentation comments on entity class and all properties
    - _Requirements: 1.1, 14.1_

  - [x] 1.3 Configure PagePermission in ApplicationDbContext
    - Register `DbSet<PagePermission>` in `ApplicationDbContext`
    - Configure table name "PagePermissions", primary key, max lengths (RoleId: 450, PagePath: 256, PageDisplayName: 256)
    - Configure unique composite index on `(RoleId, PagePath)`
    - Configure cascade delete FK relationship to `ApplicationRole`
    - Add inline comments explaining design rationale for index and cascade behavior
    - _Requirements: 1.2, 1.3, 1.4, 14.2_

  - [x] 1.4 Create and apply EF Core migration
    - Run `dotnet ef migrations add AddPagePermissions` in ApiService project
    - Verify generated migration creates the PagePermissions table with correct schema
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 2. Implement Page Permission Service in ApiService
  - [x] 2.1 Create IPagePermissionService interface
    - Create `Abstractions/IPagePermissionService.cs`
    - Define methods: `GetAllPermissionsAsync()`, `GetMyPagesAsync(string userId)`, `UpdateRolePermissionsAsync(string roleId, List<string> pagePaths)`
    - Include XML documentation on interface and all methods with `<summary>`, `<param>`, `<returns>` tags
    - _Requirements: 3.1, 3.2, 3.3, 14.1_

  - [x] 2.2 Implement PagePermissionService
    - Create `Services/PagePermissionService.cs` implementing `IPagePermissionService`
    - Inject `ApplicationDbContext`, `UserManager<ApplicationUser>`, `RoleManager<ApplicationRole>`, and `INavigationProvider`
    - `GetAllPermissionsAsync`: Query all PagePermissions grouped by role, include role name
    - `GetMyPagesAsync`: Resolve user's roles, return union of all PagePermission.PagePath records across those roles (case-insensitive)
    - `UpdateRolePermissionsAsync`: Validate roleId exists (404 if not), reject Admin role (400), reject IsSystem roles (400), validate all PagePaths exist in DefaultNavigationProvider (400 for invalid), delete existing + insert new in transaction
    - Add inline comments explaining whitelist model, full-replacement strategy, and validation logic
    - _Requirements: 1.5, 1.6, 1.7, 3.1, 3.2, 3.3, 3.4, 3.7, 3.8, 4.1, 4.4, 14.3_

  - [x] 2.3 Write property test: Whitelist Model (Property 1)
    - **Property 1: Access If and Only If Record Exists**
    - Test with random role-page combinations and random DB states using FsCheck
    - Verify CanAccess ↔ record exists equivalence with case-insensitive comparison
    - **Validates: Requirements 1.5, 1.6**

  - [x] 2.4 Write property test: Validation Rejects Invalid PagePaths (Property 2)
    - **Property 2: Validation Rejects Invalid PagePaths**
    - Generate random strings (missing "/", >256 chars, query strings, fragments)
    - Verify system rejects creation with appropriate validation errors
    - **Validates: Requirements 1.7**

  - [x] 2.5 Write property test: PUT Idempotent Full Replacement (Property 7)
    - **Property 7: PUT Idempotent Full Replacement**
    - Generate random valid path lists, call UpdateRolePermissionsAsync, verify result matches exactly
    - Repeat call and verify identical outcome
    - **Validates: Requirements 3.2**

  - [x] 2.6 Write property test: PUT Rejects Unregistered Paths (Property 8)
    - **Property 8: PUT Rejects Unregistered Paths**
    - Generate random strings not matching registered pages
    - Verify service returns 400 identifying invalid paths
    - **Validates: Requirements 3.8**

  - [x] 2.7 Write property test: Permission Union Across Roles (Property 6)
    - **Property 6: Permission Union Across Roles**
    - Generate random users with multiple roles and varying permission sets
    - Verify GetMyPagesAsync returns exact union of all role permissions
    - **Validates: Requirements 3.3**

- [x] 3. Implement PagePermissions API Controller
  - [x] 3.1 Create PagePermissionsController
    - Create `Controllers/PagePermissionsController.cs` extending `BaseController`
    - Route: `[Route("api/page-permissions")]`
    - `GET /api/page-permissions` — Admin only, returns all permissions grouped by role
    - `PUT /api/page-permissions/{roleId}` — Admin only, replaces role permissions (accepts `UpdateRolePermissionsRequest` body)
    - `GET /api/page-permissions/my-pages` — Authenticated users, returns current user's accessible pages
    - Apply `[Authorize(Roles = "Admin")]` to GET all and PUT endpoints
    - Apply `[Authorize]` to GET my-pages endpoint
    - Return appropriate status codes: 200, 400, 401, 403, 404
    - Add XML documentation comments on controller class and all action methods
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 14.1_

  - [x] 3.2 Register PagePermissionService in DI
    - Register `IPagePermissionService` → `PagePermissionService` as scoped in `Program.cs`
    - _Requirements: 3.1_

- [x] 4. Checkpoint - Ensure API layer builds and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement PagePermissionContext in Web project
  - [x] 5.1 Create IPagePermissionContext interface
    - Create `Abstractions/IPagePermissionContext.cs` in Web project
    - Define: `bool IsLoaded`, `bool CanAccess(string pagePath)`, `IReadOnlyList<string> GetAccessiblePages()`, `Task InitializeAsync()`
    - Include XML documentation on interface and all members
    - _Requirements: 5.1, 5.3, 5.4, 14.1_

  - [x] 5.2 Create ApiPagePermissionService HttpClient wrapper
    - Create `Services/ApiPagePermissionService.cs` in Web project
    - Inject `HttpClient` configured with Aspire service discovery ("https+http://apiservice")
    - Implement method to call `GET /api/page-permissions/my-pages` and return `List<string>`
    - Implement method to call `GET /api/page-permissions` and return `List<RolePermissionsDto>`
    - Implement method to call `PUT /api/page-permissions/{roleId}` with `UpdateRolePermissionsRequest`
    - Register with `UserIdentityDelegatingHandler` for auth propagation
    - Include XML documentation on class and all methods
    - _Requirements: 5.2, 8.6, 8.7, 14.1_

  - [x] 5.3 Implement PagePermissionContext
    - Create `Services/PagePermissionContext.cs` implementing `IPagePermissionContext`
    - Maintain `HashSet<string>` with `StringComparer.OrdinalIgnoreCase` for O(1) lookups
    - Define static `SystemPages` set containing all System_Page paths
    - `InitializeAsync`: Call ApiPagePermissionService, populate HashSet; on failure set empty cache and log error
    - `CanAccess`: Return true for System_Pages always, then check HashSet membership
    - `GetAccessiblePages`: Return cached list as `IReadOnlyList<string>`
    - If unauthenticated, skip API call and keep cache empty
    - Add inline comments explaining per-circuit caching strategy, O(1) lookup rationale, and System_Pages bypass
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 12.1, 12.6, 14.3_

  - [x] 5.4 Register PagePermissionContext in DI as scoped
    - Register `IPagePermissionContext` → `PagePermissionContext` as scoped in Web `Program.cs`
    - Register `ApiPagePermissionService` HttpClient with Aspire service discovery and `UserIdentityDelegatingHandler`
    - _Requirements: 5.1, 12.2_

  - [x] 5.5 Write property test: Case-Insensitive Permission Lookup (Property 5)
    - **Property 5: Case-Insensitive Permission Lookup**
    - Generate random page paths, store in context, verify CanAccess with random case mutations
    - **Validates: Requirements 5.3, 6.6, 12.1**

  - [x] 5.6 Write property test: System Pages Always Accessible (Property 4)
    - **Property 4: System Pages Always Accessible**
    - Test with empty, partial, and full cache states
    - Verify all System_Page paths return true from CanAccess regardless of cache content
    - **Validates: Requirements 5.6, 6.4**

- [x] 6. Implement PagePermissionHandler for authorization enforcement
  - [x] 6.1 Create PagePermissionRequirement and PagePermissionHandler
    - Create `Authorization/PagePermissionRequirement.cs` — empty requirement class implementing `IAuthorizationRequirement`
    - Create `Authorization/PagePermissionHandler.cs` implementing `AuthorizationHandler<PagePermissionRequirement>`
    - Inject `IPagePermissionContext`
    - Evaluation order (with inline comments): 1) Admin role → succeed, 2) System_Page → succeed, 3) CanAccess check → succeed/fail, 4) Path undetermined → succeed
    - Perform all checks synchronously using cached data
    - Add XML documentation and inline comments explaining evaluation order
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 12.3, 14.3, 14.4_

  - [x] 6.2 Register authorization handler and policy in Web Program.cs
    - Register `PagePermissionHandler` as `IAuthorizationHandler` in DI
    - Add `PagePermissionRequirement` as a fallback policy or apply to route authorization
    - Ensure PagePermissionContext.InitializeAsync is called during circuit initialization (e.g., in MainLayout or App component OnInitializedAsync)
    - _Requirements: 6.1, 12.2_

  - [x] 6.3 Write property test: Admin Immutable Full Access (Property 3)
    - **Property 3: Admin Immutable Full Access**
    - Generate random page paths and random cache states
    - Verify handler always succeeds for Admin role without consulting cache
    - **Validates: Requirements 4.1, 4.3, 6.3**

- [x] 7. Checkpoint - Ensure authorization infrastructure builds and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Implement NavMenu permission filtering
  - [x] 8.1 Modify NavMenu to filter by PagePermissionContext
    - Inject `IPagePermissionContext` into `NavMenu.razor.cs`
    - For each NavItem of type Link: show only if `CanAccess(Href)` returns true (after auth-based filtering)
    - For Group items: hide group if all children are hidden after combined filtering
    - Always render System_Page NavItems without permission check
    - While `IsLoaded` is false, render loading skeleton placeholder instead of nav links
    - On `IsLoaded` becoming true, re-render to show filtered items
    - Preserve existing `AuthorizedOnly` and `NotAuthorizedOnly` evaluation
    - Add inline comments explaining the filtering pipeline order
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 11.1, 11.5, 14.3_

  - [x] 8.2 Write property test: NavMenu Filters Inaccessible Items (Property 9)
    - **Property 9: NavMenu Filters Inaccessible Items**
    - Generate random NavItem sets and random permission states
    - Verify only accessible Link items (or System_Pages) are rendered
    - **Validates: Requirements 7.1**

  - [x] 8.3 Write property test: Empty Groups Hidden (Property 10)
    - **Property 10: Empty Groups Hidden**
    - Generate random group structures with various child visibility combinations
    - Verify groups with zero visible children are hidden
    - **Validates: Requirements 7.2**

- [x] 9. Implement Admin Page Permission Matrix UI
  - [x] 9.1 Create Page Permissions admin page component
    - Create `Components/Pages/Admin/PagePermissions/PagePermissions.razor` and `.razor.cs`
    - Route: `/admin/page-permissions`, require Admin role via `[Authorize(Roles = "Admin")]`
    - Inject `ApiPagePermissionService`, `INavigationProvider`
    - On load: fetch all roles (existing roles API), fetch all permissions (GET `/api/page-permissions`), extract Link NavItems from DefaultNavigationProvider
    - Render matrix: roles as columns, pages as rows (excluding System_Pages)
    - Admin role column: all toggles checked and disabled with tooltip "Admin always has full access"
    - Display permanent notice about changes taking effect on next refresh
    - Add XML documentation and inline comments
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.10, 8.11, 9.5, 14.1, 14.3_

  - [x] 9.2 Implement permission toggle save logic
    - On toggle: call `PUT /api/page-permissions/{roleId}` with complete updated page list
    - Show loading indicator on affected cell, disable toggles for that role during save
    - On failure: revert toggle to previous state, show error snackbar (auto-dismiss 5 seconds)
    - On success: update local state to reflect change
    - _Requirements: 8.7, 8.8, 8.9, 9.1_

  - [x] 9.3 Write property test: System Pages Excluded From Admin Matrix (Property 11)
    - **Property 11: System Pages Excluded From Admin Matrix**
    - Verify all System_Page paths are absent from the matrix page list
    - **Validates: Requirements 8.10**

- [x] 10. Update DefaultNavigationProvider and remove hardcoded attributes
  - [x] 10.1 Add Page Permissions NavItem to DefaultNavigationProvider
    - Add Link NavItem: Text "Page Permissions", Href "admin/page-permissions", Icon "material-symbols-rounded/lock"
    - Place after existing items in Administration group's Children
    - Set `AuthorizedOnly = true`
    - _Requirements: 13.1, 13.2_

  - [x] 10.2 Remove Roles property values from DefaultNavigationProvider
    - Remove all `Roles = "Admin"` assignments from NavItem definitions
    - Preserve `AuthorizedOnly = true` on items that previously had Roles set
    - Retain the `Roles` property definition on NavItem model (do not delete the property)
    - _Requirements: 11.2, 11.3, 11.4_

  - [x] 10.3 Remove hardcoded [Authorize(Roles = "...")] from page components
    - Remove `[Authorize(Roles = "Admin")]` attributes from Admin pages (AuditLog, RoleManagement, UserManagement, etc.)
    - Keep global `[Authorize]` in `_Imports.razor`
    - Keep `[AllowAnonymous]` on System_Pages (Login, Register, AccessDenied, Error, etc.)
    - _Requirements: 10.1, 10.2, 10.3, 10.4_

- [x] 11. Implement seed data for default permissions
  - [x] 11.1 Extend SeedData to seed PagePermission records
    - Modify `Data/SeedData.cs` in ApiService
    - Query `DefaultNavigationProvider.GetMainMenuItems()` to get all Link NavItems
    - For pages that previously had `Roles = "Admin"`: insert PagePermission grants for Admin role
    - For pages with `AuthorizedOnly = true` and no specific role: insert grants for all existing roles
    - Use upsert logic (check existence before insert) for idempotency
    - Use PagePath from NavItem.Href (ensure "/" prefix), PageDisplayName from NavItem.Text
    - Add inline comments explaining seed strategy
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 14.3_

- [x] 12. Final checkpoint - Ensure full solution builds and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck
- Unit tests validate specific examples and edge cases
- The implementation language is C# across all projects (ASP.NET Core, Blazor Server, EF Core)
- The system uses whitelist model: record exists = access granted, no record = denied
- Admin role always has full access; this is enforced at handler level, not via DB records
- System_Pages bypass all permission checks at both handler and context level
- All property tests use FsCheck.Xunit (v3.3.3, already installed) with minimum 2 iterations (kept low to conserve credits; increase for thorough validation later)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3"] },
    { "id": 2, "tasks": ["1.4", "2.1"] },
    { "id": 3, "tasks": ["2.2", "5.1"] },
    { "id": 4, "tasks": ["2.3", "2.4", "2.5", "2.6", "2.7", "3.1", "5.2"] },
    { "id": 5, "tasks": ["3.2", "5.3"] },
    { "id": 6, "tasks": ["5.4", "5.5", "5.6"] },
    { "id": 7, "tasks": ["6.1"] },
    { "id": 8, "tasks": ["6.2", "6.3"] },
    { "id": 9, "tasks": ["8.1", "10.1"] },
    { "id": 10, "tasks": ["8.2", "8.3", "9.1", "10.2"] },
    { "id": 11, "tasks": ["9.2", "9.3", "10.3"] },
    { "id": 12, "tasks": ["11.1"] }
  ]
}
```
