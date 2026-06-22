# Requirements Document

## Introduction

This feature enhances the existing audit log system to capture old and new values for update operations, and refactors the `LogAsync()` method signature to use a DTO contract class instead of a long parameter list. The audit log entity (`AuditLogEntry`) already has `OldValues` and `NewValues` string properties that accept JSON, but current controller calls to `LogAsync()` leave these fields null. This feature populates them for all update-type operations across UsersController, RolesController, PagePermissionsController, and AuthController.

## Glossary

- **Audit_Log_Service**: The service implementing `IAuditLogService` that persists audit entries to the database. Located in `AspireWebAppTemplate.ApiService/Services/AuditLogService.cs`.
- **AuditLogEntry**: The EF Core entity representing a single audit log record, containing OldValues and NewValues string properties for storing JSON-serialized change data. Located in `AspireWebAppTemplate.ApiService/Data/Entities/AuditLogEntry.cs`.
- **AuditLogRequest**: The new DTO contract class that encapsulates all parameters for the `LogAsync()` method, replacing the current long parameter list.
- **UsersController**: The API controller managing user CRUD, activation, and role assignment. Located in `AspireWebAppTemplate.ApiService/Controllers/UsersController.cs`.
- **RolesController**: The API controller managing role CRUD and user-role assignment. Located in `AspireWebAppTemplate.ApiService/Controllers/RolesController.cs`.
- **PagePermissionsController**: The API controller managing role-based page access permissions. Located in `AspireWebAppTemplate.ApiService/Controllers/PagePermissionsController.cs`.
- **AuthController**: The API controller handling authentication, profile updates, and password management. Located in `AspireWebAppTemplate.ApiService/Controllers/AuthController.cs`.
- **Old_Values**: A JSON-serialized string capturing the entity's field values before a modification occurred.
- **New_Values**: A JSON-serialized string capturing the entity's field values after a modification occurred.
- **Change_Set**: The subset of fields that differ between the old and new state of an entity, used to produce focused Old_Values and New_Values JSON rather than full entity snapshots.

## Requirements

### Requirement 1: AuditLogRequest DTO Contract Class

**User Story:** As a developer, I want a single DTO class that encapsulates all audit log parameters, so that the LogAsync() method signature is clean, extensible, and free from long-parameter-list code smell.

#### Acceptance Criteria

1. THE AuditLogRequest class SHALL be defined in `AspireWebAppTemplate.Core/Contracts/AuditLog/AuditLogRequest.cs` with the following properties: UserId (string, nullable), ActionType (AuditActionType, required), EntityType (AuditEntityType, required), EntityId (string, required), EntityName (string, required), Description (string, required), OldValues (string, nullable), NewValues (string, nullable), IpAddress (string, nullable)
2. THE AuditLogRequest class SHALL use property initializers to default string properties to `string.Empty` for EntityId, EntityName, and Description, matching the existing parameter behavior of the current LogAsync method
3. THE IAuditLogService interface SHALL replace the existing `LogAsync` method signature (with individual parameters) with a single `LogAsync(AuditLogRequest request)` method that accepts an AuditLogRequest parameter and returns Task
4. THE Audit_Log_Service implementation SHALL implement the `LogAsync(AuditLogRequest request)` method using the same persistence logic previously used by the old method (resolve UserDisplayName, create AuditLogEntry, persist to database)

### Requirement 2: Refactor Existing LogAsync Calls to Use AuditLogRequest

**User Story:** As a developer, I want all existing LogAsync() calls refactored to use the new AuditLogRequest DTO, so that the codebase is consistent and ready for old/new value population.

#### Acceptance Criteria

1. WHEN a controller action calls the Audit_Log_Service, THE controller action SHALL construct an AuditLogRequest instance and pass it to the `LogAsync(AuditLogRequest request)` overload
2. THE UsersController SHALL use the AuditLogRequest overload for all existing LogAsync calls in CreateUser, UpdateUser, DeleteUser, ActivateUser, DeactivateUser, SetRoles, and CreateLdapUser actions
3. THE RolesController SHALL use the AuditLogRequest overload for all existing LogAsync calls in CreateRole, UpdateRole, DeleteRole, AssignUsersToRole, and RemoveUserFromRole actions
4. THE AuthController SHALL use the AuditLogRequest overload for all existing LogAsync calls in Login, Logout, ChangePassword, and DeleteAccount actions
5. THE refactored calls SHALL produce identical AuditLogEntry records in the database as the original calls, preserving the same UserId, ActionType, EntityType, EntityId, EntityName, Description, and IpAddress values

### Requirement 3: Old/New Value Capture for User Update Operations

**User Story:** As an auditor, I want to see what changed when a user's profile is updated, so that I can review the specific modifications made to user accounts.

#### Acceptance Criteria

1. WHEN the UsersController UpdateUser action successfully updates a user, THE Audit_Log_Service SHALL receive OldValues containing a JSON object with the previous values of all fields that changed, and NewValues containing a JSON object with the new values of those same fields
2. WHEN the UsersController UpdateUser action is called, THE controller SHALL capture the user's current field values (DisplayName, FirstName, LastName, Email, PhoneNumber, JobTitle, Department, EmployeeNumber) before applying changes, compare them with the values after the update, and include only the fields that differ in OldValues and NewValues
3. WHEN the UsersController ActivateUser action is called, THE AuditLogRequest SHALL include OldValues as `{"IsActive":false}` and NewValues as `{"IsActive":true}`
4. WHEN the UsersController DeactivateUser action is called, THE AuditLogRequest SHALL include OldValues as `{"IsActive":true}` and NewValues as `{"IsActive":false}`
5. WHEN the UsersController SetRoles action is called, THE AuditLogRequest SHALL include OldValues as a JSON object with a "Roles" property containing an array of the previous role names, and NewValues with a "Roles" property containing an array of the new role names

### Requirement 4: Old/New Value Capture for Role Update Operations

**User Story:** As an auditor, I want to see what changed when a role is modified, so that I can track configuration changes to the role system.

#### Acceptance Criteria

1. WHEN the RolesController UpdateRole action successfully updates a role, THE Audit_Log_Service SHALL receive OldValues containing a JSON object with the previous values of all fields that changed (Name, DisplayName, Description, Position, IsActive), and NewValues containing a JSON object with the new values of those same fields
2. WHEN the RolesController UpdateRole action is called, THE controller SHALL capture the role's current field values before applying changes, compare them with the values after the update, and include only the fields that differ in OldValues and NewValues
3. WHEN the RolesController AssignUsersToRole action completes, THE AuditLogRequest SHALL include NewValues as a JSON object with a "UserIds" property containing an array of the successfully assigned user IDs
4. WHEN the RolesController RemoveUserFromRole action completes, THE AuditLogRequest SHALL include OldValues as a JSON object with a "UserId" property containing the removed user's ID and a "RoleName" property containing the role name

### Requirement 5: Old/New Value Capture for Page Permission Updates

**User Story:** As an auditor, I want to see what page permissions changed for a role, so that I can track access control modifications.

#### Acceptance Criteria

1. WHEN the PagePermissionsController UpdateRolePermissions action successfully updates permissions, THE Audit_Log_Service SHALL receive OldValues containing a JSON object with a "PagePaths" property listing the previous page paths for that role, and NewValues containing a JSON object with a "PagePaths" property listing the new page paths
2. THE PagePermissionsController SHALL call the Audit_Log_Service using the AuditLogRequest overload with ActionType set to SettingsChanged and EntityType set to Role
3. THE PagePermissionsController SHALL include the roleId as EntityId and the role's display name as EntityName in the AuditLogRequest

### Requirement 6: Old/New Value Capture for Auth Profile and Preference Updates

**User Story:** As an auditor, I want to see what changed when a user updates their own profile or preferences, so that I can track self-service account modifications.

#### Acceptance Criteria

1. WHEN the AuthController UpdateProfile action successfully updates a user's profile, THE Audit_Log_Service SHALL receive OldValues containing a JSON object with the previous values of all fields that changed (DisplayName, FirstName, LastName, PhoneNumber), and NewValues containing a JSON object with the new values of those same fields
2. WHEN the AuthController UpdateProfile action is called, THE controller SHALL capture the user's current field values before applying changes, compare them with the values after the update, and include only the fields that differ in OldValues and NewValues
3. WHEN the AuthController UpdatePreferences action successfully updates preferences, THE Audit_Log_Service SHALL receive OldValues containing a JSON object with the previous values of changed preference fields (Theme, TimeZoneId, DateTimeFormat), and NewValues containing a JSON object with the new values
4. WHEN the AuthController ChangePassword action succeeds, THE AuditLogRequest SHALL include NewValues as `{"PasswordChanged":true}` and OldValues SHALL remain null (password values are never logged)

### Requirement 7: JSON Serialization of Change Values

**User Story:** As a developer, I want a consistent, predictable JSON format for old/new values, so that the audit log detail view can reliably parse and display change information.

#### Acceptance Criteria

1. THE Old_Values and New_Values JSON SHALL be serialized using System.Text.Json with camelCase property naming (JsonSerializerOptions with PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
2. THE Old_Values and New_Values JSON SHALL include only the properties that changed between the old and new state, excluding unchanged fields
3. IF no fields changed during an update operation, THEN THE Audit_Log_Service SHALL still record the audit entry with OldValues and NewValues both set to null
4. THE Old_Values and New_Values JSON SHALL NOT include sensitive fields (password hashes, security stamps, authentication tokens) under any circumstance
5. THE Old_Values and New_Values JSON SHALL serialize null property values as JSON `null` rather than omitting them, so that a field being cleared is distinguishable from a field not being part of the change set

### Requirement 8: Behavioral Preservation

**User Story:** As a developer, I want the refactored LogAsync method to preserve the same runtime behavior as the original, so that audit entries remain identical in content and error handling.

#### Acceptance Criteria

1. THE `LogAsync(AuditLogRequest)` method SHALL produce AuditLogEntry records with the same field mapping as the original method: UserId maps to UserId, ActionType maps to ActionType, EntityType maps to EntityType, EntityId maps to EntityId, EntityName maps to EntityName, Description maps to Description, OldValues maps to OldValues, NewValues maps to NewValues, IpAddress maps to IpAddress
2. THE Audit_Log_Service SHALL continue to resolve UserDisplayName from the UserId using UserManager.FindByIdAsync, following the same logic as the previous method
3. THE Audit_Log_Service SHALL continue to swallow database exceptions during logging and log them at Error level, ensuring audit failures do not disrupt primary operations
