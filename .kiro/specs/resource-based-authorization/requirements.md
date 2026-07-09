# Requirements Document

## Introduction

Evolve the AspireWebAppTemplate authorization system from its current transitional state to a resource-based permission model. The system previously used hardcoded `[Authorize(Roles = "Admin")]` on admin controllers, which conflicted with the page permission system. As a transitional step, all admin controllers now use `[Authorize]` (authentication-only), and the page permission system (database-driven whitelist) controls page-level access in the Blazor frontend. This feature completes the evolution by introducing granular resource-level permissions. Roles become groupings of permissions assigned to users. API endpoints use policy-based authorization that checks for specific permission keys (e.g., "Users.Read", "Roles.Manage"). Page visibility is derived from permissions — if a role has any permission within a module, the related admin page becomes accessible. The Admin role retains super-admin behavior with implicit access to all permissions.

## Glossary

- **Permission_Entity**: The EF Core entity representing a single permission definition stored in the database. Contains a unique key (e.g., "Users.Read"), display name, module grouping, and description.
- **Role_Permission_Entity**: The EF Core join entity linking an ApplicationRole to a Permission_Entity, representing a grant of that permission to that role.
- **Permission_Service**: The backend service responsible for permission CRUD, role-permission assignment, and permission evaluation for authorization decisions.
- **Permission_Controller**: The REST API controller exposing permission management and query endpoints. Delegates all logic to Permission_Service.
- **Permission_Authorization_Handler**: The ASP.NET Core authorization handler that evaluates whether the current user holds a required permission. Checks the user's roles, loads their aggregated permissions, and satisfies the requirement if any role grants the needed permission.
- **Permission_Requirement**: The ASP.NET Core `IAuthorizationRequirement` that specifies which permission key is needed for a given endpoint or policy.
- **Permission_Context**: A per-circuit scoped service in the Web project that loads and caches the current user's effective permissions on circuit initialization. Provides synchronous access for navigation filtering and UI permission checks.
- **Api_Permission_Service**: The typed HttpClient service in the Web project that communicates with the Permission_Controller via Aspire service discovery.
- **Permission_Key**: A dot-notation string identifier for a specific permission (format: "Module.Action", e.g., "Users.Read", "Roles.Manage", "AuditLog.Export").
- **Module**: A logical grouping of related permissions (e.g., Users, Roles, AuditLog, Permissions, Announcements). Maps to a set of admin pages.
- **Permission_Management_Page**: The Blazor Server admin page that replaces the current Page Permissions page, providing a matrix UI for assigning permissions to roles.

## Requirements

### Requirement 1: Permission Data Model

**User Story:** As a developer, I want a well-defined data model for permissions and role-permission assignments, so that the system can store, query, and enforce granular access control.

#### Acceptance Criteria

1. THE Permission_Entity SHALL store the following fields: Id (int, primary key, auto-increment), Key (string, unique, max 100 characters, format "Module.Action"), DisplayName (string, max 200 characters), Module (string, max 50 characters), Description (string, nullable, max 500 characters).
2. THE Role_Permission_Entity SHALL store the following fields: RoleId (string, max 450 characters, foreign key to ApplicationRole), PermissionId (int, foreign key to Permission_Entity), with a composite unique index on (RoleId, PermissionId).
3. THE Role_Permission_Entity SHALL use cascade delete on both foreign keys so that deleting a role or permission automatically removes the assignment records.
4. THE Permission_Entity SHALL enforce a unique constraint on the Key field to prevent duplicate permission definitions.
5. THE Permission_Key format SHALL follow the pattern "Module.Action" where Module is PascalCase and Action is PascalCase (e.g., "Users.Read", "Roles.Manage", "AuditLog.Export"), and the Module field SHALL always equal the Module segment extracted from the Key field (the substring before the dot).
6. IF a permission Key value does not match the "Module.Action" PascalCase format (exactly one dot separating two PascalCase segments each between 1 and 50 characters), THEN THE System SHALL reject the operation and return a validation error indicating the expected format.
7. IF a Role_Permission assignment is attempted for a RoleId and PermissionId pair that already exists, THEN THE System SHALL reject the operation and return an error indicating the duplicate assignment.

### Requirement 2: Seed Permissions

**User Story:** As a developer, I want a predefined set of permissions seeded on application startup, so that the system has a complete permission catalog from initial deployment.

#### Acceptance Criteria

1. WHEN the application starts, THE seed process SHALL ensure the following 15 Permission_Entity records exist in the database, identified by their unique key: Users.Read, Users.Create, Users.Update, Users.Delete, Users.Activate, Roles.Read, Roles.Manage, AuditLog.Read, AuditLog.Export, Permissions.Manage, Announcements.Read, Announcements.Create, Announcements.Update, Announcements.Delete, Announcements.Publish.
2. THE seed process SHALL be idempotent — running it multiple times SHALL NOT create duplicate Permission_Entity records. Existing records with matching keys SHALL be left unchanged.
3. WHEN the seed process completes, THE Admin role SHALL have assignments to all 15 seeded permissions.
4. THE seed process SHALL NOT modify or remove existing role-permission assignments for non-Admin roles.
5. WHEN new permissions are added to the seed data in future deployments, THE seed process SHALL insert only the new Permission_Entity records and assign them to the Admin role without removing or modifying existing permission assignments for any role.
6. IF the Admin role does not exist in the database at the time of permission seeding, THEN THE seed process SHALL skip permission-to-role assignment and log a warning indicating that Admin role assignment was not performed.
7. IF a database error occurs while persisting a Permission_Entity record, THEN THE seed process SHALL log the error and continue attempting to seed the remaining permissions.

### Requirement 3: Admin Super-Role Behavior

**User Story:** As a system administrator, I want the Admin role to implicitly have all permissions, so that administrators always have full access without manual permission management.

#### Acceptance Criteria

1. WHEN a permission check is evaluated for a user holding the Admin role, THE Permission_Authorization_Handler SHALL satisfy the requirement immediately without querying Role_Permission_Entity records, using case-insensitive comparison on the role name "Admin".
2. IF the current user holds the Admin role, THEN THE Permission_Context SHALL report `HasPermission(key)` as true for every Permission_Key and `HasAnyPermissionInModule(module)` as true for every module, without issuing a database query for role-permission assignments.
3. THE Permission_Management_Page SHALL display all permissions as checked and disabled (non-editable) for the Admin role column, with a tooltip on each toggle stating "Admin always has full access".
4. IF a request is made to update permission assignments for the Admin role via the PUT endpoint, THEN THE Permission_Service SHALL reject the request and return an error response indicating that Admin role permissions are immutable and cannot be added, removed, or modified.
5. WHEN a new Permission_Entity is created (via seed or future extension), THE Permission_Authorization_Handler SHALL grant that permission to Admin-role users at evaluation time through the role-name bypass (no Role_Permission_Entity record required for the grant to take effect).

### Requirement 4: Permission-Based API Authorization

**User Story:** As a developer, I want API endpoints protected by permission-based policies instead of generic authentication checks, so that any role with the appropriate permissions can access the endpoints while users without those permissions are denied.

#### Acceptance Criteria

1. THE Permission_Authorization_Handler SHALL evaluate permission requirements by loading the set of Permission_Keys granted to the current user (computed as the union of all permissions assigned to all of the user's active roles) and checking if the set contains the required Permission_Key.
2. IF the current user holds the Admin role, THEN THE Permission_Authorization_Handler SHALL satisfy any permission requirement immediately without querying role-permission assignments.
3. THE UsersController SHALL replace `[Authorize]` with permission-based policies: GET endpoints (GetUsers, GetUser, GetRolesMetadata, LdapLookup) require "Users.Read", POST endpoints (CreateUser, CreateLdapUser, SyncLdapUsers) require "Users.Create", PUT endpoints (UpdateUser) require "Users.Update", DELETE endpoints (DeleteUser) require "Users.Delete", activation and account-management endpoints (ActivateUser, DeactivateUser, ResetPassword, SetRoles) require "Users.Activate".
4. THE RolesController SHALL replace `[Authorize]` with permission-based policies: GET endpoints (GetRoles, GetRole, GetUsersInRole) require "Roles.Read", all mutation endpoints (CreateRole, UpdateRole, DeleteRole, ActivateRole, DeactivateRole, AssignUsersToRole, RemoveUserFromRole) require "Roles.Manage".
5. THE AuditLogController SHALL replace `[Authorize]` with permission-based policies: GET query endpoints (GetAuditLog, GetAuditLogEntry) require "AuditLog.Read", the export endpoint (ExportAuditLog) requires "AuditLog.Export".
6. THE PagePermissionsController (evolving to Permission_Controller) SHALL require "Permissions.Manage" for admin mutation and query endpoints (GetAllPermissions, UpdateRolePermissions) and SHALL require only the `[Authorize]` attribute (any authenticated user) for the "my-permissions" query endpoint.
7. IF the current user does not hold any role that grants the required permission, THEN THE Permission_Authorization_Handler SHALL fail the authorization requirement, resulting in a 403 Forbidden HTTP response.
8. THE Permission_Authorization_Handler SHALL cache the resolved permission set for the current user within the scope of a single HTTP request so that multiple permission checks within the same request do not trigger repeated database queries.
9. IF the current user is not authenticated (no identity or no roles can be resolved), THEN THE Permission_Authorization_Handler SHALL fail the authorization requirement, resulting in a 401 Unauthorized HTTP response before permission evaluation occurs.

### Requirement 5: Permission Loading and Caching (API Side)

**User Story:** As a developer, I want permissions loaded efficiently on the API side, so that authorization checks do not introduce latency on every request.

#### Acceptance Criteria

1. THE Permission_Service SHALL provide a method that accepts a set of role IDs and returns the distinct set of Permission_Key strings granted to those roles, using case-insensitive comparison for Permission_Key matching.
2. THE Permission_Authorization_Handler SHALL resolve the current user's role IDs from the authenticated ClaimsPrincipal's role claims and call Permission_Service to load their effective permissions.
3. THE permission lookup SHALL be cached per HTTP request (scoped lifetime) so that multiple authorization checks within the same request reuse the same permission set without additional database queries.
4. IF the user's roles or permissions change, THE updated permissions SHALL take effect on the next API request without requiring application restart.
5. THE Permission_Service SHALL use a single database query to load all permissions for a set of role IDs (join Role_Permission_Entity with Permission_Entity).
6. IF the user has no role claims or the provided role ID set is empty, THEN THE Permission_Service SHALL return an empty permission set and the Permission_Authorization_Handler SHALL fail the requirement (deny access).
7. IF the database query to load permissions fails, THEN THE Permission_Authorization_Handler SHALL fail the requirement (deny access) and log the failure at Error level.

### Requirement 6: Page Visibility Derived from Permissions

**User Story:** As a user with specific permissions, I want to see only the admin pages relevant to my granted permissions in the navigation sidebar, so that the UI reflects my actual access.

#### Acceptance Criteria

1. THE Permission_Context SHALL load the current user's effective permissions (the set of Permission_Key strings) on circuit initialization and provide synchronous access for navigation filtering and page authorization.
2. WHEN the current user has at least one permission belonging to a module (e.g., having "Users.Read" belongs to the Users module), THE navigation filtering logic SHALL show the corresponding admin page for that module.
3. THE system SHALL define a mapping from admin page paths to their required module: "/admin/user-management" maps to the Users module, "/admin/role-management" maps to the Roles module, "/admin/audit-log" maps to the AuditLog module, "/admin/permission-management" maps to the Permissions module, "/admin/announcements" maps to the Announcements module.
4. WHILE the Admin role is assigned to the user, THE navigation SHALL display all admin pages regardless of explicit permission assignments.
5. THE Permission_Context SHALL replace the existing PagePermissionContext as the authority for navigation visibility decisions.
6. THE existing PagePermissionHandler for Blazor page authorization SHALL use the same page-to-module mapping defined in criterion 3 to determine which module a requested page belongs to, and SHALL grant access when the user holds at least one permission in that module.
7. IF the Permission_Context fails to load permissions from the API (network error or non-success response), THEN THE Permission_Context SHALL treat the cache as empty, log a warning, mark initialization as complete, and deny access to all admin pages until the next circuit initialization.
8. IF a page route is not present in the admin page-to-module mapping (e.g., "/announcements", "/account/settings"), THEN THE PagePermissionHandler SHALL grant access without requiring module permissions, preserving existing System_Page and general authenticated-page behavior.

### Requirement 7: Permission Context (Per-Circuit Caching)

**User Story:** As a developer, I want permission state cached per circuit, so that layout components and pages can check permissions synchronously without repeated API calls.

#### Acceptance Criteria

1. WHEN the circuit initializes (during MainLayout.OnInitializedAsync), THE Permission_Context SHALL load the current user's effective Permission_Key set via Api_Permission_Service and cache it in memory for synchronous access throughout the circuit lifetime.
2. THE Permission_Context SHALL provide a synchronous `HasPermission(string permissionKey)` method that returns true if the cached permission set contains the specified key OR if the user holds the Admin role, and returns false otherwise.
3. THE Permission_Context SHALL provide a synchronous `HasAnyPermissionInModule(string module)` method that returns true if the cached permission set contains any Permission_Key with the specified module prefix OR if the user holds the Admin role, and returns false otherwise.
4. THE Permission_Context SHALL expose an `IsAdmin` property that returns true when the user's claims include the Admin role, enabling fast short-circuit checks without permission set evaluation.
5. THE Permission_Context SHALL expose an `IsLoaded` property that returns false before InitializeAsync completes and true after it completes (regardless of success or failure), consistent with the existing PagePermissionContext pattern.
6. WHILE IsLoaded is false, THE Permission_Context SHALL return false from HasPermission and HasAnyPermissionInModule for non-Admin users to prevent unauthorized access before the permission cache is populated.
7. IF the Api_Permission_Service call fails during initialization, THEN THE Permission_Context SHALL log a warning, treat the permission set as empty (HasPermission returns false for non-Admin users), and set IsLoaded to true so the UI exits loading state.
8. IF the user is not authenticated at initialization time, THEN THE Permission_Context SHALL skip the API call, leave the permission cache empty, and set IsLoaded to true.

### Requirement 8: Permission Management API Endpoints

**User Story:** As a developer, I want REST API endpoints for managing permission assignments, so that the Web project can communicate with the API using the established typed HttpClient pattern.

#### Acceptance Criteria

1. THE Permission_Controller SHALL expose a GET endpoint to retrieve all defined permissions grouped by module (each group containing the module name and the list of Permission_Entity records within that module, including Key, DisplayName, and Description), accessible to users with "Permissions.Manage" permission.
2. THE Permission_Controller SHALL expose a GET endpoint accepting a role ID as a route parameter to retrieve permission assignments for that role (returning the list of granted Permission_Keys), accessible to users with "Permissions.Manage" permission.
3. IF a GET request for a specific role's permissions references a role ID that does not exist, THEN THE Permission_Controller SHALL return a 404 Not Found response.
4. THE Permission_Controller SHALL expose a PUT endpoint accepting a role ID as a route parameter and a request body containing the complete list of Permission_Keys to grant (full replacement strategy — the provided list becomes the entire permission set for that role, and an empty list removes all permissions), accessible to users with "Permissions.Manage" permission.
5. IF the PUT request body contains one or more Permission_Keys that do not match any existing Permission_Entity Key, THEN THE Permission_Service SHALL reject the request with a 400 Bad Request response indicating the invalid keys.
6. THE Permission_Controller SHALL expose a GET "my-permissions" endpoint to retrieve the current authenticated user's effective permissions as a list of granted Permission_Key strings, accessible to all authenticated users.
7. IF an administrator attempts to modify permissions for the Admin role, THEN THE Permission_Service SHALL reject the request with a 400 Bad Request response indicating that Admin permissions are immutable.
8. IF an administrator attempts to modify permissions for a system role that is not the Admin role, THEN THE Permission_Service SHALL allow the modification (non-Admin system roles can have custom permission sets).
9. THE Permission_Controller SHALL expose a GET endpoint to retrieve the permission-to-page mapping (returning a list of entries each containing a page path and the module that governs its visibility), accessible to all authenticated users, so that the Web project can determine page visibility from permissions.

### Requirement 9: Permission Management Page (Admin UI)

**User Story:** As an administrator, I want a permission management interface that replaces the current Page Permissions page, so that I can assign granular permissions to roles in a clear matrix view.

#### Acceptance Criteria

1. THE Permission_Management_Page SHALL be accessible at the route `/admin/permission-management` and require the "Permissions.Manage" permission.
2. THE Permission_Management_Page SHALL display a matrix with roles as columns (ordered by role Position ascending) and permissions grouped by module as rows. THE Permission_Management_Page SHALL use the PageContent loading wrapper during initial data fetch.
3. WHEN an administrator toggles a permission checkbox for a non-Admin role, THE Permission_Management_Page SHALL immediately call the PUT endpoint with the complete set of granted Permission_Keys for that role (full replacement strategy, auto-save per toggle).
4. THE Permission_Management_Page SHALL display all permissions as checked and disabled for the Admin role (communicating implicit full access). THE checkboxes for the Admin role SHALL NOT trigger any API calls.
5. THE Permission_Management_Page SHALL display permissions grouped by module with section headers (Users, Roles, Audit Log, Permissions, Announcements).
6. WHILE a save operation is in progress for a role, THE Permission_Management_Page SHALL disable all checkboxes for that role to prevent concurrent modifications.
7. WHEN a save operation succeeds, THE Permission_Management_Page SHALL display a success Snackbar notification.
8. IF a save operation fails, THEN THE Permission_Management_Page SHALL revert the toggled checkbox to its previous state and display the error message in an error Snackbar.
9. THE Permission_Management_Page SHALL exclude inactive roles from the column display.
10. IF the initial data load (roles or permissions) fails, THEN THE Permission_Management_Page SHALL display an error message indicating the failure and not render the matrix.

### Requirement 10: Migration from PagePermission System

**User Story:** As a developer, I want a clean migration path from the current PagePermission system to the new permission-based system, so that existing role access configurations are preserved.

#### Acceptance Criteria

1. THE migration SHALL create the Permission_Entity and Role_Permission_Entity tables in the same database migration that preserves the existing PagePermission table unmodified, so that both systems coexist without data loss.
2. THE seed process SHALL map existing PagePermission records to equivalent permission grants using the following correspondence: a role with page access to "/admin/user-management" receives Users.Read permission, "/admin/role-management" receives Roles.Read permission, "/admin/audit-log" receives AuditLog.Read permission, "/admin/page-permissions" receives Permissions.Manage permission. Roles that hold the "Admin" role SHALL receive all defined permissions regardless of their PagePermission records.
3. THE PagePermission table and its data SHALL be retained as read-only reference during the migration phase, while the PagePermissionHandler and PagePermissionContext SHALL be replaced by Permission_Authorization_Handler and Permission_Context as the sole runtime authorization mechanism.
4. THE DefaultNavigationProvider SHALL retain its existing structure — the `AuthorizedOnly = true` flag continues to indicate that permission checking is required for that nav item.
5. THE existing "Page Permissions" nav item SHALL be renamed to "Permission Management" and its href updated from "admin/page-permissions" to "admin/permission-management".
6. WHEN the migration is complete and validated, THE PagePermission table and related service code MAY be removed in a subsequent cleanup phase (not part of this feature).
7. IF the migration fails to create the new tables or seed the permission mappings, THEN THE system SHALL roll back the database transaction and retain the existing PagePermission-based authorization without disruption to current users.
8. IF a PagePermission record references a page path that has no corresponding permission grant in the mapping defined in criterion 2, THEN THE seed process SHALL skip that record and log a warning indicating the unmapped page path, without halting the overall migration.

### Requirement 11: Audit Logging for Permission Changes

**User Story:** As an administrator, I want permission changes recorded in the audit log, so that I can track who modified role permissions and when.

#### Acceptance Criteria

1. WHEN an administrator updates permission assignments for a role, THE Permission_Service SHALL create an audit log entry with ActionType=SettingsChanged, EntityType=Role, EntityId set to the role ID, and EntityName set to the role's DisplayName.
2. THE audit log entry SHALL include OldValues containing the JSON-serialized previous list of Permission_Keys and NewValues containing the JSON-serialized updated list of Permission_Keys, serialized as a camelCase JSON object with a "permissions" property containing the array of Permission_Key strings.
3. THE audit log entry SHALL include the acting administrator's UserId and IpAddress via ICurrentUserAccessor.
4. IF the audit log creation fails, THEN THE Permission_Service SHALL log the failure at Error level and continue — audit failures SHALL NOT prevent the permission update from completing.

### Requirement 12: Claims Propagation for Permissions

**User Story:** As a developer, I want the Web-to-API identity propagation to support permission-based authorization, so that API calls from the Blazor frontend are authorized correctly.

#### Acceptance Criteria

1. THE existing UserIdentityDelegatingHandler SHALL continue to forward the user's role claims to the API service without modification.
2. THE Permission_Authorization_Handler on the API side SHALL resolve permissions from the user's role claims — no additional claims or headers are required for permission evaluation.
3. THE API service SHALL determine the user's permissions by querying the database using the role IDs extracted from the forwarded role claims.
4. IF the user's claims do not include any role, THEN THE Permission_Authorization_Handler SHALL fail all permission requirements (resulting in 403).

### Requirement 13: Backward Compatibility

**User Story:** As a developer, I want the new permission system to be backward compatible with existing authentication flows, so that the transition does not break current functionality.

#### Acceptance Criteria

1. THE existing login, registration, and session management flows SHALL continue to produce the same observable outcomes (successful authentication, session creation, cookie issuance, and logout) without modification.
2. THE existing `[Authorize]` attribute (without role specification) on authenticated-only endpoints SHALL continue to require an authenticated user without modification.
3. WHILE the Admin role is assigned to a user, all pages and API endpoints previously accessible to Admin users SHALL remain accessible without requiring explicit permission record configuration.
4. THE existing notification system, announcement system, and other features SHALL continue to function with their current authorization checks until explicitly migrated to permission-based checks.
5. THE UserIdentityDelegatingHandler SHALL NOT require modification — role claims are sufficient for the API to resolve permissions.
6. THE database migration introducing permission system changes SHALL NOT alter or remove existing ASP.NET Core Identity tables (AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetRoleClaims) or their columns.
