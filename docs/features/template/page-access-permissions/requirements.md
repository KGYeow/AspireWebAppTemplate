# Requirements Document

## Introduction

The Page Access Permissions feature provides a database-driven, role-based page authorization system that replaces hardcoded `[Authorize(Roles = "...")]` attributes. Administrators configure which roles can access which pages through a UI matrix at `/admin/page-permissions`. Permissions are cached per-circuit for zero-latency navigation checks, the sidebar menu auto-filters based on the authenticated user's accessible pages, and direct URL access to unauthorized pages displays an AccessDenied view.

## Glossary

- **Page_Permission_Service**: The backend API service responsible for managing page permission records in the database and providing permission queries. Located in `AspireWebAppTemplate.ApiService`.
- **Page_Permissions_Admin_Page**: The Blazor Server UI page at `/admin/page-permissions` that displays the role × page matrix and allows administrators to toggle access. Located in `AspireWebAppTemplate.Web/Components/Pages/`.
- **PagePermission**: The EF Core entity representing a single role-page access grant, stored in the `PagePermissions` table. Located in `AspireWebAppTemplate.ApiService/Data/Entities/`.
- **PagePermissionContext**: A scoped Blazor service that loads and caches the authenticated user's accessible page paths once per circuit, providing synchronous in-memory lookups during navigation. Located in `AspireWebAppTemplate.Web/Services/`.
- **PagePermissionHandler**: A custom ASP.NET Core `AuthorizationHandler` that checks PagePermissionContext to determine whether the current user may access the requested page route. Located in `AspireWebAppTemplate.Web/`.
- **DefaultNavigationProvider**: The existing class that defines the application's navigation structure (menu items, icons, hrefs, groups). Located in `AspireWebAppTemplate.Core/Application/`.
- **NavItem**: The navigation item model containing Type, Text, Href, Icon, Roles, AuthorizedOnly, NotAuthorizedOnly, and Children properties. Located in `AspireWebAppTemplate.Core/Common/NavModels.cs`.
- **Administrator**: A user assigned the "Admin" role who has access to the Page_Permissions_Admin_Page.
- **System_Pages**: Pages that are always accessible regardless of permissions: Login, Register, AccessDenied, Error, ForgotPassword, ResetPassword, and PerformLogin.
- **Circuit**: A Blazor Server SignalR connection representing a single user session.

## Requirements

### Requirement 1: PagePermission Entity and Database Schema

**User Story:** As a developer, I want a database table that stores role-to-page access grants, so that permissions can be managed dynamically without code changes.

#### Acceptance Criteria

1. THE PagePermission entity SHALL contain the following properties: Id (int, primary key, auto-increment), RoleId (string, required, non-nullable, max 450 characters, foreign key to AspNetRoles), PagePath (string, required, non-nullable, max 256 characters, the route path of the page, must start with "/" and contain no query string or fragment), PageDisplayName (string, required, non-nullable, max 256 characters, the human-readable name of the page)
2. THE ApplicationDbContext SHALL register the PagePermission entity with the table name "PagePermissions"
3. THE ApplicationDbContext SHALL configure a unique composite index on RoleId and PagePath to prevent duplicate grants
4. THE ApplicationDbContext SHALL configure the RoleId foreign key with cascade delete behavior so that permissions are removed when a role is deleted
5. WHEN a PagePermission record exists for a given RoleId and PagePath, THE Page_Permission_Service SHALL interpret that as the role having access to that page (whitelist model), using case-insensitive comparison on PagePath
6. WHEN no PagePermission record exists for a given RoleId and PagePath, THE Page_Permission_Service SHALL interpret that as the role being denied access to that page, using case-insensitive comparison on PagePath
7. IF an attempt is made to create a PagePermission with a PagePath that does not start with "/" or exceeds 256 characters, or with a PageDisplayName that exceeds 256 characters, THEN THE System SHALL reject the operation with a validation error indicating which constraint was violated

### Requirement 2: Default Permission Seeding

**User Story:** As a developer, I want default permissions seeded on first migration that match the current hardcoded authorization attributes, so that upgrading to the new system does not break existing access.

#### Acceptance Criteria

1. WHEN the database is created or migrated, THE seed data SHALL insert PagePermission records granting the "Admin" role access to all pages listed in the DefaultNavigationProvider that currently have Roles set to "Admin"
2. WHEN the database is created or migrated, THE seed data SHALL insert PagePermission records granting all existing roles access to pages that currently have no role restriction but have AuthorizedOnly set to true
3. THE seed data SHALL use the PagePath values matching the Href property of each NavItem in DefaultNavigationProvider, prefixed with "/" if not already present
4. THE seed data SHALL use the PageDisplayName values matching the Text property of each NavItem in DefaultNavigationProvider
5. THE seed operation SHALL be idempotent — running it multiple times SHALL not create duplicate PagePermission records due to the unique composite index on RoleId and PagePath

### Requirement 3: Page Permissions API Endpoints

**User Story:** As a frontend developer, I want API endpoints to read and update page permissions, so that the admin UI and the permission cache can be populated.

#### Acceptance Criteria

1. THE Page_Permission_Service SHALL expose a GET endpoint at `/api/page-permissions` that returns all PagePermission records grouped by role, including RoleId, RoleName, and the list of granted PagePaths with their display names
2. WHEN the PUT endpoint at `/api/page-permissions/{roleId}` receives a valid request, THE Page_Permission_Service SHALL replace all existing PagePermission records for that role with the provided list of PagePaths, where an empty list removes all page permissions for that role
3. THE Page_Permission_Service SHALL expose a GET endpoint at `/api/page-permissions/my-pages` that returns the list of PagePaths accessible to the currently authenticated user based on all roles assigned to that user, returning an empty list if the user has no assigned roles or no permissions are granted
4. IF the PUT endpoint receives a request targeting a role where IsSystem is true, THEN THE Page_Permission_Service SHALL return a 400 Bad Request response with a message indicating that system role permissions cannot be modified
5. THE GET `/api/page-permissions` and PUT endpoints SHALL require the "Admin" role for access and return 403 Forbidden if the authenticated user does not hold the Admin role
6. THE GET `/api/page-permissions/my-pages` endpoint SHALL require authentication and return 401 Unauthorized if no valid authentication is provided, but SHALL NOT require a specific role
7. IF the PUT endpoint receives a roleId that does not exist in AspNetRoles, THEN THE Page_Permission_Service SHALL return a 404 Not Found response
8. IF the PUT endpoint receives a PagePath value that does not match any page registered in the DefaultNavigationProvider, THEN THE Page_Permission_Service SHALL return a 400 Bad Request response with a message indicating which PagePath values are invalid

### Requirement 4: Admin Role Immutable Full Access

**User Story:** As a system administrator, I want the Admin role to always have access to all pages, so that administrators cannot accidentally lock themselves out of the system.

#### Acceptance Criteria

1. THE Page_Permission_Service SHALL treat the "Admin" role as having access to all configurable pages regardless of PagePermission records in the database
2. THE Page_Permissions_Admin_Page SHALL display the Admin role column with all toggles rendered in an enabled (on) state that is visually distinct as non-interactive (greyed out or locked icon), and each toggle SHALL display a tooltip stating "Admin always has full access"
3. IF the current user holds the "Admin" role, THEN THE PagePermissionHandler SHALL grant access to the requested page without consulting the PagePermissionContext cache
4. IF a request is made to update page permissions for the "Admin" role via the PUT endpoint, THEN THE Page_Permission_Service SHALL reject the request and return an error response indicating that Admin role permissions cannot be modified

### Requirement 5: PagePermissionContext — Per-Circuit Permission Cache

**User Story:** As a user, I want my page permissions loaded once when my session starts and checked from memory on each navigation, so that page transitions remain fast with no database delay.

#### Acceptance Criteria

1. THE PagePermissionContext SHALL be registered as a scoped service in the Blazor Server DI container so that each circuit receives its own instance
2. WHEN a circuit is established for an authenticated user, THE PagePermissionContext SHALL call GET `/api/page-permissions/my-pages` and cache the returned list of accessible PagePaths in memory
3. THE PagePermissionContext SHALL expose a synchronous method `bool CanAccess(string pagePath)` that returns true if the given path exists in the cached accessible pages list, using case-insensitive ordinal comparison
4. THE PagePermissionContext SHALL expose a synchronous method `IReadOnlyList<string> GetAccessiblePages()` that returns the full cached list of accessible page paths
5. IF the API call to `/api/page-permissions/my-pages` fails due to a network error or non-success status code, THEN THE PagePermissionContext SHALL treat the cache as empty so that CanAccess returns false for all paths except System_Pages
6. THE PagePermissionContext SHALL treat all System_Pages as accessible regardless of cached permissions
7. WHILE the PagePermissionContext has not yet completed loading permissions, THE PagePermissionContext SHALL return false for all CanAccess checks except System_Pages
8. IF the user is unauthenticated, THEN THE PagePermissionContext SHALL not call the API and SHALL return false for all CanAccess checks except System_Pages

### Requirement 6: Authorization Enforcement via PagePermissionHandler

**User Story:** As a system owner, I want unauthorized page access attempts to be blocked and shown an AccessDenied page, so that the permission configuration is actually enforced.

#### Acceptance Criteria

1. THE PagePermissionHandler SHALL implement IAuthorizationHandler and be registered in the authorization service pipeline
2. WHEN a user navigates to a page route, THE PagePermissionHandler SHALL extract the target page path from the route data in the authorization resource and evaluate access in the following order: Admin role check, then System_Page check, then PagePermissionContext cached permission check
3. WHEN the user holds the "Admin" role, THE PagePermissionHandler SHALL succeed the authorization requirement without consulting PagePermissionContext
4. WHEN the page route matches a System_Page path, THE PagePermissionHandler SHALL succeed the authorization requirement without consulting PagePermissionContext
5. WHEN the PagePermissionContext indicates the user cannot access the page, THE PagePermissionHandler SHALL fail the authorization requirement causing Blazor to redirect to the AccessDenied view
6. THE PagePermissionHandler SHALL perform path comparisons using case-insensitive ordinal matching
7. IF the page path cannot be determined from the authorization resource, THEN THE PagePermissionHandler SHALL succeed the authorization requirement to avoid blocking non-page resources

### Requirement 7: Navigation Menu Filtering

**User Story:** As a user, I want the sidebar menu to only show pages I can access, so that I am not confused by links to pages I cannot use.

#### Acceptance Criteria

1. WHEN the navigation menu is rendered, THE NavMenu component SHALL hide (not render) any NavItem of type Link whose Href value causes PagePermissionContext.CanAccess to return false
2. WHEN a NavItem of type Group has zero visible children remaining after both authentication-based filtering and permission-based filtering are applied, THE NavMenu component SHALL hide the entire group including its header
3. THE NavMenu component SHALL evaluate NavItem.AuthorizedOnly and NavItem.NotAuthorizedOnly visibility first, and then apply PagePermissionContext.CanAccess filtering only to items that pass authentication-based visibility
4. THE NavMenu component SHALL always render NavItems whose Href matches a System_Page path without consulting PagePermissionContext.CanAccess
5. WHEN the PagePermissionContext has not yet loaded (initial render before async load completes), THE NavMenu component SHALL render no navigation links and SHALL display a loading skeleton placeholder in the navigation area until permissions become available
6. WHEN the PagePermissionContext finishes loading, THE NavMenu component SHALL re-render to display the filtered set of permitted navigation items within 1 render cycle

### Requirement 8: Admin Page — Role × Page Permission Matrix

**User Story:** As an administrator, I want a visual matrix showing all roles and all pages with toggles, so that I can quickly see and modify which roles have access to which pages.

#### Acceptance Criteria

1. THE Page_Permissions_Admin_Page SHALL be accessible at the route "/admin/page-permissions"
2. THE Page_Permissions_Admin_Page SHALL require the "Admin" role for access
3. THE Page_Permissions_Admin_Page SHALL display a matrix with roles as columns and pages as rows
4. THE Page_Permissions_Admin_Page SHALL read the list of available pages by extracting all NavItems of type Link from the DefaultNavigationProvider (including those nested inside Group items), displaying each Link NavItem's Text as the row label and Href as the page path, and excluding Header, Divider, and Group container items themselves
5. THE Page_Permissions_Admin_Page SHALL read the list of roles from the existing roles API endpoint
6. WHEN the Page_Permissions_Admin_Page is loaded, THE Page_Permissions_Admin_Page SHALL call GET `/api/page-permissions` to retrieve current permission grants and display each cell as checked if a PagePermission record exists for that role-page combination, or unchecked otherwise
7. WHEN an administrator toggles a permission cell, THE Page_Permissions_Admin_Page SHALL call PUT `/api/page-permissions/{roleId}` with the complete updated list of page paths for that role (full replacement of all granted paths)
8. WHILE a permission update is being saved, THE Page_Permissions_Admin_Page SHALL display a loading indicator on the affected cell and disable further toggles for that role until the save completes
9. IF a permission update fails, THEN THE Page_Permissions_Admin_Page SHALL revert the toggle to its previous state and display an error notification that auto-dismisses after 5 seconds
10. THE Page_Permissions_Admin_Page SHALL exclude System_Pages from the matrix since those are always accessible
11. THE Page_Permissions_Admin_Page SHALL display the Admin role column with all checkboxes checked and disabled (non-interactive), with a tooltip on each disabled checkbox indicating that Admin access cannot be modified

### Requirement 9: Permission Cache Invalidation

**User Story:** As an administrator, I want permission changes to take effect when affected users refresh their page or start a new session, so that the system reflects updates without requiring application restart.

#### Acceptance Criteria

1. WHEN an administrator saves permission changes, THE Page_Permission_Service SHALL persist the changes to the database within the same HTTP request before returning the response to the caller
2. THE PagePermissionContext SHALL load permissions once per circuit and not refresh automatically during an active session
3. WHEN a user starts a new circuit (page refresh, new tab, or re-login), THE PagePermissionContext SHALL call GET `/api/page-permissions/my-pages` and cache the returned list, replacing any previously held permission data
4. IF the permission loading API call fails during circuit initialization, THEN THE PagePermissionContext SHALL deny access to all pages except System_Pages and display an error notification indicating that permissions could not be loaded
5. THE Page_Permissions_Admin_Page SHALL permanently display a notice on the page informing the administrator that changes take effect on next page refresh or new session for affected users

### Requirement 10: Removal of Hardcoded Role Attributes

**User Story:** As a developer, I want to remove the existing `[Authorize(Roles = "Admin")]` attributes from page components and replace them with the PagePermissionHandler system, so that authorization is managed consistently through the database.

#### Acceptance Criteria

1. WHEN the PagePermissionHandler is active, THE existing `[Authorize(Roles = "Admin")]` attributes on page components SHALL be removed
2. THE global `[Authorize]` attribute in `_Imports.razor` SHALL remain to enforce authentication for all pages
3. THE `[AllowAnonymous]` attributes on System_Pages SHALL remain unchanged
4. AFTER the migration, THE PagePermissionHandler SHALL be the sole mechanism for role-based page access control in the Web project

### Requirement 11: DefaultNavigationProvider Roles Property Deprecation

**User Story:** As a developer, I want the NavItem.Roles property to no longer drive menu visibility, so that navigation filtering is consistent with the database-driven permission model.

#### Acceptance Criteria

1. WHILE the PagePermissionContext service is registered and has completed its initial permission load, THE NavMenu component SHALL determine link visibility by calling PagePermissionContext.CanAccess for each NavItem's Href value instead of passing NavItem.Roles to AuthorizeView
2. THE DefaultNavigationProvider SHALL define no NavItem instances with a non-null Roles property value; all role-based visibility assignments (e.g., Roles = "Admin" on the Administration group) SHALL be removed
3. THE NavItem model class SHALL retain the Roles property definition with its existing type and accessor so that external consumers referencing the property continue to compile, but the NavMenu component SHALL NOT read or evaluate NavItem.Roles for any rendering or visibility decision
4. THE DefaultNavigationProvider SHALL preserve the AuthorizedOnly = true property on NavItems that previously combined AuthorizedOnly with a Roles value (e.g., the Administration group), so that those items remain hidden from anonymous users
5. THE NavMenu component SHALL continue to evaluate NavItem.AuthorizedOnly and NavItem.NotAuthorizedOnly properties to control authenticated-vs-anonymous visibility independently of PagePermissionContext role-based filtering

### Requirement 12: Performance — Zero Navigation Latency

**User Story:** As a user, I want page-to-page navigation to remain instant after permissions are loaded, so that the new authorization system does not degrade the user experience.

#### Acceptance Criteria

1. THE PagePermissionContext SHALL complete all permission checks via in-memory lookup with no network or database calls after the initial permission load has completed
2. WHEN a circuit is established for an authenticated user, THE PagePermissionContext SHALL load permissions in a single API call during circuit initialization before the first navigation authorization check is performed
3. THE PagePermissionHandler SHALL perform authorization checks synchronously using the cached data from PagePermissionContext without awaiting any asynchronous operations
4. WHEN measured against the baseline without the page permission system, THE page navigation time SHALL not increase by more than 5ms additional latency per navigation event, measured as the elapsed time of the PagePermissionHandler authorization check in isolation
5. IF the PagePermissionContext has not yet completed its initial permission load when a navigation occurs, THEN THE PagePermissionHandler SHALL deny access to non-System_Pages until the cache is populated, consistent with the behavior defined in Requirement 5 criterion 7
6. THE PagePermissionContext SHALL store cached page paths in a HashSet or equivalent O(1) lookup structure so that CanAccess checks do not degrade with the number of cached permissions

### Requirement 13: Navigation Menu Integration

**User Story:** As an administrator, I want the Page Permissions admin page to appear in the navigation menu under the Administration group, so that I can easily access it.

#### Acceptance Criteria

1. THE DefaultNavigationProvider SHALL include a NavItem of type Link with Text "Page Permissions", Href "admin/page-permissions", and Icon "material-symbols-rounded/lock" appended after the existing items within the Administration group's Children collection
2. THE "Page Permissions" NavItem SHALL have AuthorizedOnly set to true

### Requirement 14: Code Documentation and Maintainability

**User Story:** As a developer, I want all page permission code to include complete XML documentation comments and inline comments, so that the codebase remains maintainable and easy to understand.

#### Acceptance Criteria

1. ALL public classes, interfaces, methods, properties, and enum values SHALL include XML documentation comments using `<summary>`, `<param>`, `<returns>`, and `<remarks>` tags as appropriate
2. ALL EF Core configuration blocks (entity configuration, index definitions, relationship setup) SHALL include inline comments explaining the design rationale
3. ALL complex logic blocks (permission resolution, cache loading, matrix rendering, authorization decisions) SHALL include inline comments explaining the algorithm or business rule being implemented
4. THE PagePermissionHandler authorization logic SHALL include inline comments explaining the evaluation order: Admin role check → System_Page check → cached permission check
