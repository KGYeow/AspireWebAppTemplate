# Implementation Plan: Audit Log Old/New Values

## Overview

This plan implements two complementary enhancements to the audit log system: (1) an `AuditLogRequest` DTO to replace the long-parameter-list `LogAsync()` method, and (2) old/new value capture for all update operations across controllers. Implementation proceeds bottom-up — DTO and utility first, then service layer, then controller-by-controller migration with change tracking.

## Tasks

- [x] 1. Create AuditLogRequest DTO and AuditChangeHelper utility
  - [x] 1.1 Create the AuditLogRequest DTO class
    - Create `AspireWebAppTemplate.Core/Contracts/AuditLog/AuditLogRequest.cs`
    - Define all properties: UserId, ActionType, EntityType, EntityId, EntityName, Description, OldValues, NewValues, IpAddress
    - Set default values for EntityId, EntityName, and Description to `string.Empty`
    - _Requirements: 1.1, 1.2_

  - [x] 1.2 Create the AuditChangeHelper utility class
    - Create `AspireWebAppTemplate.ApiService/Utilities/AuditChangeHelper.cs`
    - Implement static `JsonSerializerOptions` with camelCase naming and `JsonIgnoreCondition.Never`
    - Implement `Snapshot<T>` method for field capture
    - Implement `ComputeChanges` method for diffing before/after dictionaries
    - Implement `Serialize` method for direct object serialization
    - _Requirements: 7.1, 7.2, 7.3, 7.5_

  - [x] 1.3 Write property tests for AuditChangeHelper.ComputeChanges
    - **Property 2: ComputeChanges Includes Only and All Differing Fields**
    - **Validates: Requirements 3.1, 3.2, 4.1, 4.2, 6.1, 6.2, 6.3, 7.2, 7.3**

  - [x] 1.4 Write property tests for serialization round-trip
    - **Property 3: Serialization Round-Trip Preserves Values**
    - **Validates: Requirements 3.5, 4.3, 4.4, 5.1, 7.1**

  - [x] 1.5 Write property tests for camelCase naming
    - **Property 4: CamelCase Naming in Serialized Output**
    - **Validates: Requirements 7.1**

  - [x] 1.6 Write property tests for null value preservation
    - **Property 5: Null Values Preserved as JSON Null**
    - **Validates: Requirements 7.5**

  - [x] 1.7 Write property tests for AuditLogRequest default values
    - **Property 6: AuditLogRequest Default Property Values**
    - **Validates: Requirements 1.2**

- [x] 2. Refactor IAuditLogService and AuditLogService
  - [x] 2.1 Update IAuditLogService interface
    - Replace the existing `LogAsync` method signature (with individual parameters) with `LogAsync(AuditLogRequest request)`
    - Remove the old method signature entirely
    - Add using directive for `AspireWebAppTemplate.Core.Contracts.AuditLog`
    - _Requirements: 1.3_

  - [x] 2.2 Implement LogAsync(AuditLogRequest) in AuditLogService
    - Replace the existing method implementation with one accepting `AuditLogRequest`
    - Map all DTO properties to `AuditLogEntry` entity fields including OldValues and NewValues
    - Preserve existing behavior: resolve UserDisplayName, swallow DbUpdateException with error logging
    - _Requirements: 1.4, 8.1, 8.2, 8.3_

  - [x] 2.3 Write property test for LogAsync field mapping
    - **Property 1: LogAsync Field Mapping Correctness**
    - **Validates: Requirements 1.4, 2.5, 8.1**

- [x] 3. Refactor UsersController to use AuditLogRequest with old/new values
  - [x] 3.1 Define UserAuditFields and refactor existing LogAsync calls
    - Define static `UserAuditFields` array for snapshot capture (DisplayName, FirstName, LastName, Email, PhoneNumber, JobTitle, Department, EmployeeNumber)
    - Refactor CreateUser, DeleteUser, CreateLdapUser actions to construct `AuditLogRequest` instances
    - _Requirements: 2.1, 2.2_

  - [x] 3.2 Add old/new value capture to UpdateUser action
    - Snapshot user fields before mutation using `AuditChangeHelper.Snapshot`
    - Snapshot user fields after mutation
    - Compute changes using `AuditChangeHelper.ComputeChanges`
    - Pass OldValues and NewValues in the `AuditLogRequest`
    - _Requirements: 3.1, 3.2_

  - [x] 3.3 Add old/new value capture to ActivateUser and DeactivateUser actions
    - ActivateUser: set OldValues to `{"isActive":false}` and NewValues to `{"isActive":true}`
    - DeactivateUser: set OldValues to `{"isActive":true}` and NewValues to `{"isActive":false}`
    - Use `AuditChangeHelper.Serialize` for consistent JSON formatting
    - _Requirements: 3.3, 3.4_

  - [x] 3.4 Add old/new value capture to SetRoles action
    - Capture previous role names as OldValues with "Roles" array property
    - Capture new role names as NewValues with "Roles" array property
    - _Requirements: 3.5_

  - [x] 3.5 Write unit tests for UsersController audit changes
    - Test ActivateUser produces correct `{"isActive":false}` / `{"isActive":true}` JSON
    - Test DeactivateUser produces correct `{"isActive":true}` / `{"isActive":false}` JSON
    - Test UpdateUser only includes changed fields
    - Test sensitive fields never appear in snapshots
    - _Requirements: 3.3, 3.4, 7.4_

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Refactor RolesController to use AuditLogRequest with old/new values
  - [x] 5.1 Define RoleAuditFields and refactor existing LogAsync calls
    - Define static `RoleAuditFields` array for snapshot capture (Name, DisplayName, Description, Position, IsActive)
    - Refactor CreateRole, DeleteRole actions to construct `AuditLogRequest` instances
    - _Requirements: 2.1, 2.3_

  - [x] 5.2 Add old/new value capture to UpdateRole action
    - Snapshot role fields before mutation using `AuditChangeHelper.Snapshot`
    - Snapshot role fields after mutation
    - Compute changes using `AuditChangeHelper.ComputeChanges`
    - Pass OldValues and NewValues in the `AuditLogRequest`
    - _Requirements: 4.1, 4.2_

  - [x] 5.3 Add old/new value capture to AssignUsersToRole and RemoveUserFromRole actions
    - AssignUsersToRole: set NewValues with "UserIds" array of successfully assigned user IDs
    - RemoveUserFromRole: set OldValues with "UserId" and "RoleName" properties
    - _Requirements: 4.3, 4.4_

  - [x] 5.4 Write unit tests for RolesController audit changes
    - Test UpdateRole only includes changed fields
    - Test AssignUsersToRole captures correct NewValues format
    - Test RemoveUserFromRole captures correct OldValues format
    - _Requirements: 4.1, 4.3, 4.4_

- [x] 6. Refactor PagePermissionsController to use AuditLogRequest with old/new values
  - [x] 6.1 Refactor UpdateRolePermissions to use AuditLogRequest with old/new values
    - Capture previous page paths for the role as OldValues with "PagePaths" array
    - Capture new page paths as NewValues with "PagePaths" array
    - Set ActionType to SettingsChanged and EntityType to Role
    - Include roleId as EntityId and role display name as EntityName
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 6.2 Write unit tests for PagePermissionsController audit changes
    - Test correct ActionType (SettingsChanged) and EntityType (Role) are used
    - Test OldValues/NewValues contain "PagePaths" array property
    - _Requirements: 5.1, 5.2, 5.3_

- [x] 7. Refactor AuthController to use AuditLogRequest with old/new values
  - [x] 7.1 Refactor existing LogAsync calls in Login, Logout, DeleteAccount
    - Convert Login, Logout, ChangePassword, DeleteAccount to use `AuditLogRequest` instances
    - _Requirements: 2.1, 2.4_

  - [x] 7.2 Add old/new value capture to UpdateProfile action
    - Snapshot profile fields before mutation (DisplayName, FirstName, LastName, PhoneNumber)
    - Snapshot profile fields after mutation
    - Compute changes using `AuditChangeHelper.ComputeChanges`
    - Pass OldValues and NewValues in the `AuditLogRequest`
    - _Requirements: 6.1, 6.2_

  - [x] 7.3 Add old/new value capture to UpdatePreferences action
    - Snapshot preference fields before mutation (Theme, TimeZoneId, DateTimeFormat)
    - Snapshot preference fields after mutation
    - Compute changes using `AuditChangeHelper.ComputeChanges`
    - Pass OldValues and NewValues in the `AuditLogRequest`
    - _Requirements: 6.3_

  - [x] 7.4 Add old/new value capture to ChangePassword action
    - Set NewValues to `{"passwordChanged":true}` and OldValues to null
    - _Requirements: 6.4_

  - [x] 7.5 Write unit tests for AuthController audit changes
    - Test ChangePassword produces `{"passwordChanged":true}` with null OldValues
    - Test UpdateProfile only includes changed fields
    - Test UpdatePreferences only includes changed fields
    - _Requirements: 6.1, 6.3, 6.4_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties using FsCheck.Xunit (already in project)
- Unit tests validate specific examples and edge cases using xUnit + Moq (already in project)
- The design uses C# throughout — all implementations target the existing .NET/ASP.NET Core project structure
- The old `LogAsync` method signature is removed entirely (no obsolete overload) since all callers are migrated in the same change

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "1.4", "1.5", "1.6", "1.7", "2.1"] },
    { "id": 2, "tasks": ["2.2"] },
    { "id": 3, "tasks": ["2.3", "3.1", "5.1", "6.1", "7.1"] },
    { "id": 4, "tasks": ["3.2", "3.3", "3.4", "5.2", "5.3", "7.2", "7.3", "7.4"] },
    { "id": 5, "tasks": ["3.5", "5.4", "6.2", "7.5"] }
  ]
}
```
