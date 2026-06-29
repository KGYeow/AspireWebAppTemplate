# Implementation Plan: Controller-Service Refactor

## Overview

Refactor four fat controllers (`AuditLogController`, `RolesController`, `UsersController`, `AuthController`) to the thin-controller / full-service-layer pattern. New service interfaces and implementations are created, a cross-cutting `ICurrentUserAccessor` is introduced, and controllers are slimmed to delegate all business logic. Property-based tests validate correctness properties defined in the design.

## Tasks

- [x] 1. Create ICurrentUserAccessor and CurrentUserAccessor
  - [x] 1.1 Create the ICurrentUserAccessor interface in `ApiService/Abstractions/ICurrentUserAccessor.cs`
    - Define read-only properties: `UserId` (string?), `UserName` (string?), `IpAddress` (string?)
    - Add XML documentation describing the contract
    - _Requirements: 1.1, 1.4, 1.5_

  - [x] 1.2 Create the CurrentUserAccessor implementation in `ApiService/Services/CurrentUserAccessor.cs`
    - Inject `IHttpContextAccessor`
    - Read `UserId` from `ClaimTypes.NameIdentifier` claim
    - Read `UserName` from `Identity.Name`
    - Read `IpAddress` from `X-Client-Ip` header first, fall back to `Connection.RemoteIpAddress`
    - Return null for all properties when HttpContext or user is unavailable (no exceptions)
    - _Requirements: 1.2, 1.4, 1.5_

  - [x] 1.3 Register CurrentUserAccessor in DI in `ApiService/Program.cs`
    - Add `builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>()`
    - Ensure `AddHttpContextAccessor()` is called (verify or add)
    - _Requirements: 1.3, 8.1, 8.6_

  - [x] 1.4 Update UserIdentityDelegatingHandler in Web project to forward X-Client-Ip header
    - Read client IP from `HttpContext.Connection.RemoteIpAddress` (or `CircuitUserContext` cached IP)
    - Add `X-Client-Ip` header to outbound requests to the API service
    - _Requirements: 1.6_

  - [x] 1.5 Write property test for CurrentUserAccessor claim extraction (Property 1)
    - **Property 1: CurrentUserAccessor claim extraction round-trip**
    - For any valid UserId, UserName, and IpAddress strings set on an HttpContext, verify the accessor returns those exact values
    - **Validates: Requirements 1.2**

- [x] 2. Create IAuditLogQueryService and AuditLogQueryService
  - [x] 2.1 Create the IAuditLogQueryService interface in `ApiService/Abstractions/IAuditLogQueryService.cs`
    - Define `GetPagedAsync(AuditLogQueryParams)` returning `PagedResult<AuditLogEntryDto>`
    - Define `GetByIdAsync(Guid)` returning `AuditLogEntryDto` (throws `KeyNotFoundException`)
    - Define `GetForExportAsync(AuditLogQueryParams)` returning `List<AuditLogEntryDto>`
    - Add XML documentation with exception annotations
    - _Requirements: 2.1, 2.3, 2.5_

  - [x] 2.2 Create the AuditLogQueryService implementation in `ApiService/Services/AuditLogQueryService.cs`
    - Inject `ApplicationDbContext`
    - Implement consolidated `ApplyFilters(IQueryable, AuditLogQueryParams)` private method
    - Implement `GetPagedAsync` with pagination, ordering by timestamp descending, `AsNoTracking`, and Select projection to DTO
    - Implement `GetByIdAsync` with `KeyNotFoundException` on miss
    - Implement `GetForExportAsync` capped at `ExportDefaults.MaxExportRows`
    - Implement case-insensitive search against UserDisplayName, EntityName, Description, EntityId
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [x] 2.3 Register AuditLogQueryService in DI in `ApiService/Program.cs`
    - Add `builder.Services.AddScoped<IAuditLogQueryService, AuditLogQueryService>()`
    - _Requirements: 8.2, 8.6_

  - [x] 2.4 Write property test for audit log pagination invariants (Property 2)
    - **Property 2: Audit log pagination invariants**
    - For any AuditLogQueryParams with page >= 0 and pageSize > 0, verify Items.Count <= PageSize, Page == queryParams.Page, TotalCount >= Items.Count
    - **Validates: Requirements 2.1**

  - [x] 2.5 Write property test for audit log search filter correctness (Property 3)
    - **Property 3: Audit log search filter correctness**
    - For any non-empty search term, every returned entry contains the term in UserDisplayName, EntityName, Description, or EntityId
    - **Validates: Requirements 2.2**

  - [x] 2.6 Write property test for audit log lookup round-trip (Property 4)
    - **Property 4: Audit log lookup round-trip**
    - For any existing audit log entry, GetByIdAsync returns a DTO with all fields matching the persisted entity
    - **Validates: Requirements 2.3**

  - [x] 2.7 Write property test for audit log export row cap (Property 5)
    - **Property 5: Audit log export row cap**
    - For any AuditLogQueryParams, GetForExportAsync returns at most ExportDefaults.MaxExportRows entries
    - **Validates: Requirements 2.5**

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Create IRoleService and RoleService
  - [x] 4.1 Create the RoleAssignmentResult DTO in `Core/Contracts/Roles/RoleAssignmentResult.cs`
    - Define `Success` (int) and `Failed` (int) properties
    - _Requirements: 3.3_

  - [x] 4.2 Create the IRoleService interface in `ApiService/Abstractions/IRoleService.cs`
    - Define CRUD methods: `GetAllAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`
    - Define activation methods: `ActivateAsync`, `DeactivateAsync`
    - Define user-role methods: `AssignUsersAsync`, `RemoveUserAsync`, `GetUsersInRoleAsync`
    - Add XML documentation with exception annotations
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 4.3 Create the RoleService implementation in `ApiService/Services/RoleService.cs`
    - Inject `RoleManager<ApplicationRole>`, `UserManager<ApplicationUser>`, `IAuditLogService`, `ICurrentUserAccessor`
    - Implement full CRUD with business rule guards (system role protection, user-assigned guard on delete)
    - Implement activation/deactivation with system role check
    - Implement user assignment (returns RoleAssignmentResult) and removal (with RequiresMinimumUser guard)
    - Implement audit logging with old/new value tracking via AuditChangeHelper on updates
    - Throw `KeyNotFoundException` for non-existent roles, `InvalidOperationException` for rule violations
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8, 3.9, 3.10, 3.11_

  - [x] 4.4 Register RoleService in DI in `ApiService/Program.cs`
    - Add `builder.Services.AddScoped<IRoleService, RoleService>()`
    - _Requirements: 8.3, 8.6_

  - [x] 4.5 Write property test for role CRUD round-trip (Property 6)
    - **Property 6: Role CRUD round-trip**
    - For any valid CreateRoleRequest, creating then reading back returns matching Name, DisplayName, Description, Position, IsActive
    - **Validates: Requirements 3.1**

  - [x] 4.6 Write property test for role activation state change (Property 7)
    - **Property 7: Role activation state change**
    - For any non-system role, ActivateAsync yields IsActive=true, DeactivateAsync yields IsActive=false
    - **Validates: Requirements 3.2**

  - [x] 4.7 Write property test for role user assignment count invariant (Property 8)
    - **Property 8: Role user assignment count invariant**
    - For any role and user ID array, AssignUsersAsync result satisfies Success + Failed == userIds.Length
    - **Validates: Requirements 3.3**

  - [x] 4.8 Write property test for system role protection (Property 9)
    - **Property 9: System role protection**
    - For any role with IsSystem=true, UpdateAsync, DeleteAsync, ActivateAsync, DeactivateAsync all throw InvalidOperationException
    - **Validates: Requirements 3.7**

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Create IUserService and UserService
  - [x] 6.1 Create the IUserService interface in `ApiService/Abstractions/IUserService.cs`
    - Define CRUD methods: `CreateAsync`, `GetByIdAsync`, `UpdateAsync`, `DeleteAsync`
    - Define `SearchAsync` with pagination and optional search term
    - Define activation methods: `ActivateAsync`, `DeactivateAsync`
    - Define `SetRolesAsync` for replacing all role assignments
    - Define `GetRolesMetadataAsync` for UI role list
    - Define LDAP methods: `LdapLookupAsync`, `CreateLdapUserAsync`, `SyncLdapUsersAsync` (returns IAsyncEnumerable)
    - Add XML documentation with exception annotations
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

  - [x] 6.2 Create the UserService implementation in `ApiService/Services/UserService.cs`
    - Inject `UserManager<ApplicationUser>`, `RoleManager<ApplicationRole>`, `IAuditLogService`, `ICurrentUserAccessor`, `ILdapAuthService`
    - Implement CRUD with business rule guards (self-deletion/deactivation, last-admin protection)
    - Implement search/pagination with case-insensitive filter across UserName, DisplayName, Email, FirstName, LastName, Department
    - Implement role set replacement via `SetRolesAsync`
    - Implement LDAP operations (lookup, create with duplicate check, sync returning IAsyncEnumerable<LdapSyncProgressItem>)
    - Implement audit logging for all mutating operations
    - Throw `KeyNotFoundException` for non-existent users, `InvalidOperationException` for rule violations
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9, 4.10, 4.11_

  - [x] 6.3 Register UserService in DI in `ApiService/Program.cs`
    - Add `builder.Services.AddScoped<IUserService, UserService>()`
    - _Requirements: 8.4, 8.6_

  - [x] 6.4 Write property test for user CRUD round-trip (Property 11)
    - **Property 11: User CRUD round-trip**
    - For any valid CreateUserRequest, creating then reading back returns matching Email, DisplayName, IsActive
    - **Validates: Requirements 4.1**

  - [x] 6.5 Write property test for user search filter correctness (Property 12)
    - **Property 12: User search filter correctness**
    - For any non-empty search term, every UserDto in the result contains the term in UserName, DisplayName, Email, FirstName, LastName, or Department
    - **Validates: Requirements 4.2**

  - [x] 6.6 Write property test for user role set replacement (Property 13)
    - **Property 13: User role set replacement**
    - For any user and valid role name array, SetRolesAsync then reading roles yields a set equal to the input
    - **Validates: Requirements 4.4**

- [x] 7. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Create IAuthManagementService and AuthManagementService
  - [x] 8.1 Create the IAuthManagementService interface in `ApiService/Abstractions/IAuthManagementService.cs`
    - Define profile methods: `GetProfileAsync`, `UpdateProfileAsync`, `UpdatePreferencesAsync`
    - Define password methods: `ChangePasswordAsync`, `SetPasswordAsync`
    - Define email methods: `GetEmailAsync`, `ChangeEmailAsync`, `SendVerificationEmailAsync`
    - Define 2FA methods: `Get2faStatusAsync`, `GetAuthenticatorSetupAsync`, `VerifyAuthenticatorAsync`, `Disable2faAsync`, `GenerateRecoveryCodesAsync`, `ResetAuthenticatorAsync`
    - Define data/account methods: `DownloadPersonalDataAsync`, `DeleteAccountAsync`
    - Define external login methods: `GetExternalLoginsAsync`, `RemoveExternalLoginAsync`
    - Define passkey methods: `GetPasskeysAsync`, `DeletePasskeyAsync`, `RenamePasskeyAsync`
    - Add XML documentation with exception annotations
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.10_

  - [x] 8.2 Create the AuthManagementService implementation in `ApiService/Services/AuthManagementService.cs`
    - Inject `UserManager<ApplicationUser>`, `SignInManager<ApplicationUser>`, `IAuditLogService`, `ICurrentUserAccessor`
    - All methods operate on the currently authenticated user from `ICurrentUserAccessor.UserId`
    - Implement profile get/update, preferences update
    - Implement password change (verify current password) and set-password (verify no existing password)
    - Implement email change initiation and confirmation
    - Implement full 2FA lifecycle (setup, verify, disable, reset, recovery codes)
    - Implement personal data download (all `[PersonalData]` properties + Id, UserName, Email, etc.)
    - Implement account deletion (verify password)
    - Implement external login list/remove and passkey list/delete/rename
    - Implement audit logging for security-sensitive operations
    - Throw `InvalidOperationException` for rule violations (wrong password, 2FA not enabled, etc.)
    - _Requirements: 5.1–5.15_

  - [x] 8.3 Register AuthManagementService in DI in `ApiService/Program.cs`
    - Add `builder.Services.AddScoped<IAuthManagementService, AuthManagementService>()`
    - _Requirements: 8.5, 8.6_

  - [x] 8.4 Write property test for profile and preferences round-trip (Property 14)
    - **Property 14: Profile and preferences round-trip**
    - For any authenticated user, updating profile/preferences then reading back returns matching updated fields
    - **Validates: Requirements 5.1, 5.2, 5.3**

  - [x] 8.5 Write property test for personal data download completeness (Property 15)
    - **Property 15: Personal data download completeness**
    - For any user with populated profile fields, DownloadPersonalDataAsync returns JSON containing all [PersonalData] properties plus Id, UserName, Email, EmailConfirmed, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled
    - **Validates: Requirements 5.7**

- [x] 9. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Refactor AuditLogController to thin controller
  - [x] 10.1 Refactor AuditLogController to delegate to IAuditLogQueryService
    - Remove `ApplicationDbContext` injection
    - Inject only `IAuditLogQueryService` and `IExcelExportService`
    - Replace inline EF Core queries with calls to `_queryService.GetPagedAsync`, `_queryService.GetByIdAsync`, `_queryService.GetForExportAsync`
    - Add try/catch exception-to-HTTP mapping for `KeyNotFoundException` → 404
    - Preserve all existing route attributes, HTTP methods, and authorization requirements
    - _Requirements: 2.7, 2.8, 6.1, 9.1, 9.2_

- [x] 11. Refactor RolesController to thin controller
  - [x] 11.1 Refactor RolesController to delegate to IRoleService
    - Remove `RoleManager`, `UserManager`, and `ApplicationDbContext` injections
    - Inject only `IRoleService`
    - Replace all inline business logic with service method calls
    - Add try/catch exception-to-HTTP mapping (KeyNotFoundException → 404, InvalidOperationException → 400)
    - Preserve all existing route attributes, HTTP methods, and authorization requirements
    - _Requirements: 3.12, 3.13, 6.2, 7.1, 7.2, 7.3, 7.4, 9.1, 9.2_

- [x] 12. Refactor UsersController to thin controller
  - [x] 12.1 Refactor UsersController to delegate to IUserService
    - Remove `UserManager`, `RoleManager`, and `ApplicationDbContext` injections
    - Inject only `IUserService`
    - Replace all inline business logic with service method calls
    - Wire LDAP sync endpoint to stream `IAsyncEnumerable<LdapSyncProgressItem>` as NDJSON
    - Add try/catch exception-to-HTTP mapping
    - Preserve all existing route attributes, HTTP methods, and authorization requirements
    - _Requirements: 4.12, 4.13, 6.3, 7.1, 7.2, 7.3, 7.4, 9.1, 9.2, 9.5_

- [x] 13. Refactor AuthController to thin controller
  - [x] 13.1 Refactor AuthController to delegate to IAuthManagementService
    - Remove `UserManager`, `SignInManager`, and `ApplicationDbContext` injections
    - Inject only `IAuthManagementService`, `ILoginService`, and `IRegisterService`
    - Delegate profile/preferences/password/email/2FA/data/external-login/passkey operations to `_authManagementService`
    - Keep login/register/2FA-login/recovery/token/forgot-password/reset-password/confirm-email delegating to existing `_loginService` and `_registerService`
    - Add try/catch exception-to-HTTP mapping
    - Preserve all existing route attributes, HTTP methods, and authorization requirements
    - _Requirements: 5.16, 5.17, 5.18, 6.4, 7.1, 7.2, 7.3, 7.4, 9.1, 9.2_

- [x] 14. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 15. Cross-cutting property tests
  - [x] 15.1 Write property test for non-existent entity throws KeyNotFoundException (Property 10)
    - **Property 10: Non-existent entity throws KeyNotFoundException**
    - For any string ID not corresponding to an existing role or user, GetByIdAsync/UpdateAsync/DeleteAsync/ActivateAsync/DeactivateAsync throw KeyNotFoundException
    - **Validates: Requirements 3.10, 4.1**

  - [x] 15.2 Write property test for audit logging invariant (Property 16)
    - **Property 16: Audit logging invariant**
    - For any mutating service operation, verify IAuditLogService.LogAsync is called exactly once with UserId matching ICurrentUserAccessor.UserId and IpAddress matching ICurrentUserAccessor.IpAddress
    - **Validates: Requirements 3.11, 4.11, 5.15**

  - [x] 15.3 Write property test for exception-to-HTTP-status mapping (Property 17)
    - **Property 17: Exception-to-HTTP-status mapping**
    - For any controller action, KeyNotFoundException → 404, InvalidOperationException/ArgumentException → 400, success → 200/201/204
    - **Validates: Requirements 7.1, 7.2, 7.3, 7.4**

- [x] 16. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- All test files go in `AspireWebAppTemplate.Tests/ControllerServiceRefactor/` directory
- FsCheck.Xunit 3.3.3 with `[Property(MaxTest = 2)]` per coding standards
- SQLite in-memory for service tests requiring EF Core, Moq for external dependencies
- The `BaseController` is NOT modified — controllers continue using its `CurrentUserId`, `CurrentUserName`, `ClientIpAddress` properties

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "4.1"] },
    { "id": 1, "tasks": ["1.2", "2.1", "4.2", "6.1", "8.1"] },
    { "id": 2, "tasks": ["1.3", "1.4", "2.2", "4.3", "6.2", "8.2"] },
    { "id": 3, "tasks": ["1.5", "2.3", "4.4", "6.3", "8.3"] },
    { "id": 4, "tasks": ["2.4", "2.5", "2.6", "2.7", "4.5", "4.6", "4.7", "4.8", "6.4", "6.5", "6.6", "8.4", "8.5"] },
    { "id": 5, "tasks": ["10.1", "11.1", "12.1", "13.1"] },
    { "id": 6, "tasks": ["15.1", "15.2", "15.3"] }
  ]
}
```
