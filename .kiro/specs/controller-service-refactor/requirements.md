# Requirements Document

## Introduction

Refactor the four "fat" controllers in the ApiService project (`AuditLogController`, `RolesController`, `UsersController`, `AuthController`) to follow the thin-controller / full-service-layer pattern already established by `NotificationController` and `PagePermissionsController`. All business logic, database access, audit logging, and entity mapping moves into dedicated service classes. A new `ICurrentUserAccessor` scoped service provides the authenticated user's identity to services without parameter-passing overhead.

## Glossary

- **Controller**: An ASP.NET Core API controller class that handles HTTP concerns only — request parsing, status code mapping, and delegating to services.
- **Service**: A scoped class registered in DI that owns all business logic, database queries, entity mutations, audit logging, and DTO projection for a domain area.
- **Thin_Controller**: A controller that contains zero business logic — it delegates entirely to one or more service interfaces.
- **Fat_Controller**: A controller that currently contains inline business logic, direct DbContext queries, or entity manipulation that should reside in a service.
- **ICurrentUserAccessor**: A scoped service interface that exposes the authenticated user's `UserId`, `UserName`, and `IpAddress` properties, backed by `IHttpContextAccessor`.
- **CurrentUserAccessor**: The implementation of `ICurrentUserAccessor` that reads claims and connection info from the current `HttpContext`.
- **IAuditLogQueryService**: A service interface responsible for querying, filtering, and exporting audit log entries (extends the existing `IAuditLogService` which handles write operations).
- **IRoleService**: A service interface responsible for full role CRUD, activation/deactivation, user-role assignment, and related business rules.
- **IUserService**: A service interface responsible for full user CRUD, search/pagination, activation/deactivation, role management, and LDAP synchronization.
- **IAuthManagementService**: A service interface responsible for profile management, preferences, password operations, email changes, two-factor authentication, personal data download, and account deletion.
- **BaseController**: The abstract controller base class providing `CurrentUserId`, `CurrentUserName`, and `ClientIpAddress` helper properties.
- **AuditChangeHelper**: A utility class that provides `Snapshot`, `ComputeChanges`, and `Serialize` methods for audit old/new value tracking.
- **ApplicationDbContext**: The EF Core database context for the application.

## Requirements

### Requirement 1: Current User Accessor Service

**User Story:** As a service-layer developer, I want a scoped service that provides the authenticated user's identity, so that services can perform audit logging and ownership checks without receiving userId/ipAddress as method parameters.

#### Acceptance Criteria

1. THE CurrentUserAccessor SHALL implement the ICurrentUserAccessor interface exposing `UserId` (string?), `UserName` (string?), and `IpAddress` (string?) read-only properties.
2. WHEN an HTTP request is being processed, THE CurrentUserAccessor SHALL read `UserId` from `ClaimTypes.NameIdentifier`, `UserName` from `Identity.Name`, and `IpAddress` from the `X-Client-Ip` request header (forwarded by the Web project's UserIdentityDelegatingHandler), falling back to `HttpContext.Connection.RemoteIpAddress?.ToString()` only if the header is absent.
3. THE CurrentUserAccessor SHALL be registered as a scoped service in the DI container.
4. IF no authenticated user is present in the HttpContext, THEN THE CurrentUserAccessor SHALL return null for all three properties.
5. IF HttpContext is not available (e.g., outside an HTTP request scope), THEN THE CurrentUserAccessor SHALL return null for all three properties without throwing an exception.
6. THE UserIdentityDelegatingHandler in the Web project SHALL forward the end-user's client IP address as an `X-Client-Ip` header on outbound requests to the API service, reading it from the Web project's HttpContext.Connection.RemoteIpAddress (cached in CircuitUserContext for post-SSR availability).

### Requirement 2: Audit Log Query Service

**User Story:** As an administrator, I want audit log querying and export to be handled by a dedicated service, so that the AuditLogController contains no EF Core queries or filter construction logic.

#### Acceptance Criteria

1. THE IAuditLogQueryService SHALL expose a method that accepts an AuditLogQueryParams object and returns a PagedResult<AuditLogEntryDto> containing the matching entries ordered by timestamp descending, plus the total count, current page index, and page size.
2. WHEN a search term is provided, THE IAuditLogQueryService SHALL filter entries by case-insensitive partial match against UserDisplayName, EntityName, Description, and EntityId fields.
3. THE IAuditLogQueryService SHALL expose a method to retrieve a single audit log entry by its Guid identifier.
4. IF a single audit log entry is requested and no entry exists with the specified identifier, THEN THE IAuditLogQueryService SHALL throw a KeyNotFoundException.
5. THE IAuditLogQueryService SHALL expose a method that accepts an AuditLogQueryParams object and returns filtered audit log entries ordered by timestamp descending, capped at ExportDefaults.MaxExportRows.
6. THE IAuditLogQueryService SHALL consolidate filter construction logic into a single reusable method, eliminating the filter duplication present in the current controller.
7. THE AuditLogController SHALL delegate all query, lookup, and export-data-retrieval operations to IAuditLogQueryService without containing any EF Core queries or filter logic.
8. THE AuditLogController SHALL NOT inject ApplicationDbContext directly.

### Requirement 3: Role Service

**User Story:** As an administrator, I want all role management business logic in a dedicated service, so that the RolesController handles only HTTP concerns.

#### Acceptance Criteria

1. THE IRoleService SHALL expose methods for creating, reading (single role by ID and all roles with user counts), updating, and deleting roles.
2. THE IRoleService SHALL expose methods for activating and deactivating roles.
3. THE IRoleService SHALL expose a method for assigning one or more users to a role, returning a result containing the count of successful and failed assignments.
4. THE IRoleService SHALL expose a method for removing a single user from a role.
5. THE IRoleService SHALL expose a method for listing users assigned to a specific role.
6. WHEN a create or update operation fails due to an identity validation error (e.g., duplicate name), THE IRoleService SHALL throw an InvalidOperationException containing the concatenated identity error descriptions.
7. WHEN a delete, update, activation, or deactivation is attempted on a role where IsSystem is true, THE IRoleService SHALL throw an InvalidOperationException indicating that system roles cannot be modified.
8. WHEN a role deletion is attempted and users are still assigned to the role, THE IRoleService SHALL throw an InvalidOperationException indicating users must be unassigned first.
9. WHEN removal of a user from a role is attempted and the role has RequiresMinimumUser set to true with only one user remaining, THE IRoleService SHALL throw an InvalidOperationException indicating at least one user must remain assigned.
10. WHEN a role operation references a role ID that does not exist, THE IRoleService SHALL throw a KeyNotFoundException.
11. THE IRoleService SHALL perform audit logging for create, update, delete, assign, and unassign operations using ICurrentUserAccessor for the acting user identity, with old/new value change tracking on update operations via AuditChangeHelper.
12. THE RolesController SHALL delegate all operations to IRoleService and contain no business logic, no RoleManager usage, and no ApplicationDbContext queries.
13. THE RolesController SHALL NOT inject RoleManager, UserManager, or ApplicationDbContext directly.

### Requirement 4: User Service

**User Story:** As an administrator, I want all user management business logic in a dedicated service, so that the UsersController handles only HTTP concerns.

#### Acceptance Criteria

1. THE IUserService SHALL expose methods for creating a user, retrieving a single user by ID, updating a user, and deleting a user.
2. THE IUserService SHALL expose a method for searching and paginating users with optional filter criteria matching against username, display name, email, first name, last name, and department fields.
3. THE IUserService SHALL expose methods for activating and deactivating user accounts.
4. THE IUserService SHALL expose a method for assigning roles to a user that replaces all existing role assignments with the provided set of role names.
5. THE IUserService SHALL expose methods for LDAP user lookup by identifier, LDAP user creation from directory attributes, and LDAP bulk synchronization of all LDAP-sourced users.
6. THE IUserService SHALL expose a method for retrieving role metadata used in user management UI.
7. WHEN a user deletion is attempted on the last active administrator, THE IUserService SHALL throw an InvalidOperationException to prevent lockout.
8. IF a user deletion or deactivation is attempted on the currently authenticated user, THEN THE IUserService SHALL throw an InvalidOperationException indicating that self-deletion or self-deactivation is not permitted.
9. WHEN LDAP synchronization is performed, THE IUserService SHALL return a streaming-compatible result as a sequence of per-user progress items, each containing the total user count, current index, username, and an update outcome indicating whether the user was updated, unchanged, or failed.
10. IF LDAP user creation is attempted and a user with the same username or email already exists, THEN THE IUserService SHALL throw an InvalidOperationException indicating the duplicate.
11. THE IUserService SHALL perform audit logging for all mutating operations using ICurrentUserAccessor for the acting user identity.
12. THE UsersController SHALL delegate all operations to IUserService and contain no business logic, no UserManager usage, and no ApplicationDbContext queries.
13. THE UsersController SHALL NOT inject UserManager, RoleManager, or ApplicationDbContext directly.

### Requirement 5: Auth Management Service

**User Story:** As an authenticated user, I want all account management business logic in a dedicated service, so that the AuthController handles only HTTP concerns for profile, password, email, 2FA, and account operations.

#### Acceptance Criteria

1. THE IAuthManagementService SHALL expose a method for retrieving the current user's profile information.
2. THE IAuthManagementService SHALL expose a method for updating the current user's profile.
3. THE IAuthManagementService SHALL expose a method for updating the current user's preferences (theme, timezone, date/time format).
4. THE IAuthManagementService SHALL expose methods for changing a password (requiring the current password) and setting an initial password (for accounts that do not yet have one).
5. THE IAuthManagementService SHALL expose methods for initiating and confirming email changes.
6. THE IAuthManagementService SHALL expose methods for enabling, disabling, and resetting two-factor authentication (including authenticator setup, verification, and recovery code generation).
7. THE IAuthManagementService SHALL expose a method for downloading the current user's personal data.
8. THE IAuthManagementService SHALL expose a method for deleting the current user's account.
9. THE IAuthManagementService SHALL expose methods for managing external logins (list, remove).
10. THE IAuthManagementService SHALL expose methods for managing passkeys (list, remove, rename).
11. WHEN a password change is attempted with an incorrect current password, THE IAuthManagementService SHALL throw an InvalidOperationException with a descriptive message.
12. WHEN account deletion is attempted with an incorrect password, THE IAuthManagementService SHALL throw an InvalidOperationException with a descriptive message.
13. IF a set-password operation is attempted on an account that already has a password, THEN THE IAuthManagementService SHALL throw an InvalidOperationException with a descriptive message.
14. IF a disable-2FA or generate-recovery-codes operation is attempted when two-factor authentication is not currently enabled, THEN THE IAuthManagementService SHALL throw an InvalidOperationException with a descriptive message.
15. THE IAuthManagementService SHALL perform audit logging for security-sensitive operations (password change, email change, 2FA enable/disable/reset, account deletion) using ICurrentUserAccessor.
16. THE AuthController SHALL delegate all profile, preference, password, email, 2FA, personal data, account deletion, external login, and passkey operations to IAuthManagementService.
17. THE AuthController SHALL continue to delegate login, registration, 2FA-login, recovery-code-login, token-validation, forgot-password, reset-password, and confirm-email operations to the existing ILoginService and IRegisterService interfaces.
18. THE AuthController SHALL NOT inject UserManager, SignInManager, or ApplicationDbContext directly.

### Requirement 6: Controller Injection Constraints

**User Story:** As a project maintainer, I want a clear rule that prevents controllers from regressing to fat-controller patterns, so that the architecture remains consistent.

#### Acceptance Criteria

1. THE AuditLogController SHALL inject only IAuditLogQueryService and IExcelExportService.
2. THE RolesController SHALL inject only IRoleService.
3. THE UsersController SHALL inject only IUserService.
4. THE AuthController SHALL inject only IAuthManagementService, ILoginService, and IRegisterService.
5. THE BaseController SHALL NOT be modified — controllers continue to access CurrentUserId, CurrentUserName, and ClientIpAddress from its existing properties.

### Requirement 7: Exception-to-HTTP-Status Mapping

**User Story:** As an API consumer, I want consistent HTTP status code mapping from service exceptions, so that error responses are predictable across all refactored endpoints.

#### Acceptance Criteria

1. WHEN a service method throws KeyNotFoundException, THE Controller SHALL return HTTP 404 with the exception message.
2. WHEN a service method throws InvalidOperationException, THE Controller SHALL return HTTP 400 with the exception message.
3. WHEN a service method throws ArgumentException, THE Controller SHALL return HTTP 400 with the exception message.
4. WHEN a service method completes successfully, THE Controller SHALL return the appropriate success status code (200, 201, or 204) with the result data.

### Requirement 8: Service Registration

**User Story:** As a developer, I want all new services registered consistently in the DI container, so that the application startup is predictable.

#### Acceptance Criteria

1. THE Application SHALL register ICurrentUserAccessor with its CurrentUserAccessor implementation as a scoped service.
2. THE Application SHALL register IAuditLogQueryService with its implementation as a scoped service.
3. THE Application SHALL register IRoleService with its implementation as a scoped service.
4. THE Application SHALL register IUserService with its implementation as a scoped service.
5. THE Application SHALL register IAuthManagementService with its implementation as a scoped service.
6. THE Application SHALL register all new services in the existing `Program.cs` service registration section.

### Requirement 9: Behavioral Preservation

**User Story:** As a user of the API, I want the refactoring to preserve all existing endpoint behavior, so that no functionality is lost or altered.

#### Acceptance Criteria

1. THE refactored endpoints SHALL maintain identical HTTP method, route, request body, query parameter, and response body contracts as the current implementation.
2. THE refactored endpoints SHALL maintain identical authorization requirements (roles, policies) as the current implementation.
3. THE refactored endpoints SHALL produce identical audit log entries (same action types, entity types, old/new values) as the current implementation.
4. THE refactored endpoints SHALL maintain identical error response messages and status codes for all known error conditions.
5. WHEN the LDAP sync endpoint is called, THE IUserService SHALL produce streaming-compatible results matching the current SSE (Server-Sent Events) behavior.
