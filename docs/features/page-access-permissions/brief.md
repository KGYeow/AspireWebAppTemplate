# Feature Brief: Page Access Permissions

## Overview

Implement a **page-level access control system** where admins can configure which roles can access which pages through the UI — without code changes. When a new role is created, an admin can assign page access via a settings page instead of a developer modifying `[Authorize(Roles = "...")]` attributes.

## Problem Statement

Currently, page access is hardcoded via `[Authorize(Roles = "Admin")]` attributes on Razor pages. When a new role is added (e.g., "Manager", "Auditor"), a developer must:
1. Modify the `[Authorize]` attributes in code
2. Rebuild and redeploy the application

This is inflexible for enterprise environments where roles change frequently.

## Desired Behavior

1. **Admin UI** (`/admin/page-permissions`) — A page showing a matrix of roles × pages, with toggles to grant/revoke access
2. **Database-driven** — Permissions stored in a `PagePermissions` table (RoleId, PagePath)
3. **Cached per-circuit** — Permissions loaded once at login/circuit start, checked from memory (no DB hit per navigation)
4. **Navigation filtering** — The sidebar/nav menu only shows pages the user's roles can access
5. **Authorization enforcement** — If a user navigates to a page they don't have access to (via URL), they see AccessDenied

## Scope

- **Page-level only** — "Can this role see this page?" (not fine-grained like "can edit field X")
- **Role-based** — Permissions are assigned to roles, not individual users
- **All pages configurable** — Every `@page` route in the app appears in the admin matrix
- **System pages excluded** — Login, Register, AccessDenied, Error pages are always accessible

## Technical Approach (Suggested)

### Database
- `PagePermission` entity: `Id`, `RoleId` (FK → AspNetRoles), `PagePath`, `PageDisplayName`
- If a row exists for RoleId + PagePath → that role can access that page
- If no row exists → access denied (whitelist model)
- Seed default permissions matching current `[Authorize]` attributes and `DefaultNavigationProvider.Roles` values
- Migration to create the table

### Relationship to DefaultNavigationProvider
- Currently `NavItem.Roles` controls menu visibility (hardcoded in C#)
- After implementation: `NavItem.Roles` is removed; visibility comes from `PagePermission` table
- `NavItem.AuthorizedOnly` / `NotAuthorizedOnly` remain (controls "logged in vs anonymous", not role-specific)
- The menu structure (text, icon, href, children, headers, dividers) stays in `DefaultNavigationProvider` — these are design decisions, not admin-configurable
- Only the **visibility per role** moves to the database
- The admin page-permissions UI reads the available pages list FROM `DefaultNavigationProvider` (no separate page registry needed) — it shows each nav item's path and display name as the "assignable pages"

### API
- `GET /api/page-permissions` — Returns all page-permission mappings
- `PUT /api/page-permissions/{roleId}` — Updates page access for a role
- `GET /api/page-permissions/my-pages` — Returns accessible pages for current user (used for nav filtering)

### Frontend (Web project)
- Custom `AuthorizationHandler` that checks cached page permissions instead of static role attributes
- Scoped `PagePermissionContext` service — loads user's accessible pages once per circuit
- Navigation provider filters menu items based on accessible pages
- Admin page with role × page matrix UI (MudDataGrid or custom grid)

### Performance
- Permissions cached in a scoped service (loaded once per circuit start)
- No DB queries on page navigation — checked from in-memory cache
- Cache invalidated only when admin changes permissions (via event, same pattern as `UserTimeZoneContext.OnInitialized`)

## Out of Scope (for initial implementation)
- Per-user overrides (only role-level)
- Fine-grained action permissions (view/edit/delete per entity)
- API endpoint authorization (only page/UI level)
- Multi-tenant permission isolation

## Acceptance Criteria

1. Admin can view a matrix of all roles × all pages
2. Admin can toggle access on/off for any role-page combination
3. Changes take effect on next login (or when user refreshes the page)
4. Navigation menu only shows pages the user can access
5. Direct URL access to unauthorized pages shows AccessDenied
6. Performance: no measurable difference in page load time vs current static approach
7. System roles (Admin) always have access to all pages (cannot be restricted)
8. Current `[Authorize(Roles = "Admin")]` attributes can be replaced with the new system

## Related Files (Current Authorization)
- `AspireWebAppTemplate.Web/Components/_Imports.razor` — Global `[Authorize]`
- `AspireWebAppTemplate.Web/Components/Pages/AuditLog/Index.razor` — `[Authorize(Roles = "Admin")]`
- `AspireWebAppTemplate.Web/Components/Pages/UserManagement/Index.razor` — Uses global `[Authorize]`
- `AspireWebAppTemplate.Web/Components/Pages/Account/Auth/_Imports.razor` — `[AllowAnonymous]` for auth pages
- `AspireWebAppTemplate.Core/Common/NavModels.cs` — Navigation items (need filtering)
- `AspireWebAppTemplate.Web/Components/Layout/NavMenu.razor.cs` — Renders nav items (needs permission check)

## Notes
- This feature was discussed and decided in a previous session as suitable for the template project
- The "cached per-circuit" approach was chosen to avoid performance concerns
- The event-based notification pattern (like `UserTimeZoneContext.OnInitialized`) should be used for cache invalidation
