# Implementation Plan: Parameter List DTO Refactoring

## Overview

This plan refactors 8 service and API client methods from long parameter lists into single DTO parameters. The work is organized by creating new DTOs first, then updating interfaces and implementations, then updating callers, and finally adding property-based and unit tests. Each task builds incrementally on previous steps so nothing is left unwired.

## Tasks

- [ ] 1. Create new DTO classes in Core/Contracts
  - [ ] 1.1 Create TrySendEmailRequest and SendEmailRequest in Core/Contracts/Email
    - Create `AspireWebAppTemplate.Core/Contracts/Email/TrySendEmailRequest.cs` as a sealed class with properties: `UserId` (string), `RecipientEmail` (string?), `Category` (NotificationCategory), `EmailType` (EmailType), `Variables` (Dictionary<string, string>)
    - Create `AspireWebAppTemplate.Core/Contracts/Email/SendEmailRequest.cs` as a sealed class with properties: `EmailType` (EmailType), `RecipientEmail` (string), `Variables` (Dictionary<string, string>)
    - Include full XML documentation on classes and all properties
    - _Requirements: 1.1, 1.2, 8.1, 8.2_

  - [ ] 1.2 Create RegisterRequest in Core/Contracts/Auth
    - Create `AspireWebAppTemplate.Core/Contracts/Auth/RegisterRequest.cs` as a sealed class with properties: `Email` (string), `Password` (string), `ConfirmEmailBaseUri` (string), `ReturnUrl` (string?)
    - Include full XML documentation on class and all properties
    - _Requirements: 4.1, 4.2_

  - [ ] 1.3 Create UserQueryParams in Core/Contracts/Users
    - Create `AspireWebAppTemplate.Core/Contracts/Users/UserQueryParams.cs` as a sealed class with properties: `Page` (int, default 0), `PageSize` (int, default 10), `SearchTerm` (string?)
    - Include full XML documentation on class and all properties
    - _Requirements: 5.1, 5.2_

- [ ] 2. Refactor IEmailService and EmailService (TrySendEmailAsync and SendEmailAsync)
  - [ ] 2.1 Update IEmailService interface signatures
    - Change `TrySendEmailAsync` to accept a single `TrySendEmailRequest` parameter
    - Change `SendEmailAsync` to accept a single `SendEmailRequest` parameter
    - Remove old multi-parameter signatures
    - _Requirements: 1.1, 1.5, 8.1, 8.5_

  - [ ] 2.2 Update EmailService implementation for SendEmailAsync
    - Refactor `SendEmailAsync` to extract `EmailType`, `RecipientEmail`, and `Variables` from the `SendEmailRequest` DTO
    - Preserve all existing behavior: template resolution, SMTP send, no-op logging fallback, `InvalidOperationException` on SMTP failure, `KeyNotFoundException` on missing template
    - _Requirements: 8.1, 8.3_

  - [ ] 2.3 Update EmailService implementation for TrySendEmailAsync
    - Refactor `TrySendEmailAsync` to extract properties from the `TrySendEmailRequest` DTO
    - Update internal call to `SendEmailAsync` to construct and pass a `SendEmailRequest` instance
    - Preserve all existing behavior: preference checking, error swallowing, failure logging
    - _Requirements: 1.1, 1.3, 8.4_

  - [ ] 2.4 Update all callers of TrySendEmailAsync (RegisterService, UserService, LoginService)
    - Update each caller to construct a `TrySendEmailRequest` instance instead of passing individual parameters
    - _Requirements: 1.4_

  - [ ] 2.5 Update Identity integration methods calling SendEmailAsync
    - Update any internal callers within EmailService (Identity `IEmailSender` methods) to construct `SendEmailRequest` instances
    - _Requirements: 8.4_

  - [ ]* 2.6 Write property test for DTO serialization round-trip (TrySendEmailRequest and SendEmailRequest)
    - **Property 1: DTO Serialization Round-Trip**
    - **Validates: Requirements 1.1, 8.1**

  - [ ]* 2.7 Write property test for TrySendEmailAsync never throws
    - **Property 2: TrySendEmailAsync Never Throws**
    - **Validates: Requirements 1.3**

  - [ ]* 2.8 Write property test for SendEmailAsync behavior preservation
    - **Property 10: SendEmailAsync Behavior Preservation**
    - **Validates: Requirements 8.3**

- [ ] 3. Refactor ILoginService and LoginService
  - [ ] 3.1 Update ILoginService interface signature
    - Change `ValidateAndGenerateTokenAsync` to accept a single `LoginRequest` parameter (reuse existing `Core/Contracts/Auth/LoginRequest`)
    - Remove old 4-parameter signature
    - _Requirements: 2.1, 2.6_

  - [ ] 3.2 Update LoginService implementation
    - Extract `Email`, `Password`, `RememberMe`, and `ReturnUrl` from the `LoginRequest` DTO
    - Preserve all existing behavior: credential validation, lockout tracking, lockout email notification, 2FA detection, token generation
    - Handle null `ReturnUrl` as equivalent to default redirect path
    - _Requirements: 2.2, 2.3, 2.5_

  - [ ] 3.3 Update AuthController caller for LoginService
    - Pass the already-deserialized `LoginRequest` object directly to `ValidateAndGenerateTokenAsync` instead of destructuring
    - _Requirements: 2.4_

  - [ ]* 3.4 Write property test for LoginService behavior preservation
    - **Property 3: LoginService Behavior Preservation**
    - **Validates: Requirements 2.2, 2.3**

- [ ] 4. Refactor ILdapLoginService and LdapLoginService
  - [ ] 4.1 Update ILdapLoginService interface signature
    - Change `ValidateAndGenerateTokenAsync` to accept a single `LoginRequest` parameter (reuse existing `Core/Contracts/Auth/LoginRequest`)
    - Remove old 4-parameter signature
    - _Requirements: 3.1, 3.6_

  - [ ] 4.2 Update LdapLoginService implementation
    - Use `LoginRequest.Email` as the LDAP identifier, extract `Password`, `RememberMe`, `ReturnUrl` from DTO properties
    - Preserve all existing behavior: LDAP bind authentication, auto-provisioning, attribute syncing, token generation
    - Handle null `ReturnUrl` as equivalent to default redirect path
    - _Requirements: 3.2, 3.3, 3.5_

  - [ ] 4.3 Update AuthController caller for LdapLoginService
    - Pass the `LoginRequest` object directly to the service without destructuring
    - _Requirements: 3.4_

  - [ ]* 4.4 Write property test for LdapLoginService behavior preservation
    - **Property 4: LdapLoginService Behavior Preservation**
    - **Validates: Requirements 3.2, 3.3**

- [ ] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Refactor IRegisterService and RegisterService
  - [ ] 6.1 Update IRegisterService interface signature
    - Change `RegisterUserAsync` to accept a single `RegisterRequest` parameter
    - Remove old 4-parameter signature
    - _Requirements: 4.1_

  - [ ] 6.2 Update RegisterService implementation
    - Extract `Email`, `Password`, `ConfirmEmailBaseUri`, `ReturnUrl` from the `RegisterRequest` DTO
    - Preserve all existing behavior: user creation, default role assignment, email confirmation token generation, welcome email sending
    - Return failed `RegisterResult` without creating user if Email or Password is null/empty
    - _Requirements: 4.3, 4.5_

  - [ ] 6.3 Update AuthController caller for RegisterService
    - Construct a `RegisterRequest` instance from the incoming HTTP request data and pass to `RegisterUserAsync`
    - _Requirements: 4.4_

  - [ ]* 6.4 Write property test for RegisterService behavior preservation
    - **Property 5: RegisterService Behavior Preservation**
    - **Validates: Requirements 4.3**

  - [ ]* 6.5 Write property test for RegisterService rejects invalid input
    - **Property 6: RegisterService Rejects Invalid Input**
    - **Validates: Requirements 4.5**

  - [ ]* 6.6 Write property test for RegisterRequest serialization round-trip
    - **Property 1: DTO Serialization Round-Trip (RegisterRequest)**
    - **Validates: Requirements 4.1**

- [ ] 7. Refactor IUserService.SearchAsync and related callers
  - [ ] 7.1 Update IUserService interface signature
    - Change `SearchAsync` to accept a single `UserQueryParams` parameter
    - Remove old 3-parameter signature
    - _Requirements: 5.1_

  - [ ] 7.2 Update UserService implementation
    - Extract `Page`, `PageSize`, `SearchTerm` from the `UserQueryParams` DTO
    - Preserve all existing behavior: case-insensitive partial matching, pagination, display name ordering
    - _Requirements: 5.3_

  - [ ] 7.3 Update UsersController to bind UserQueryParams from query string
    - Use `[FromQuery]` to bind query string parameters to the `UserQueryParams` DTO and pass to the service
    - _Requirements: 5.4_

  - [ ] 7.4 Update ApiUserService (Web project) to accept UserQueryParams
    - Change `GetUsersAsync` to accept a `UserQueryParams` parameter
    - Serialize properties as query string parameters in the HTTP GET request to `/api/users`
    - Update all callers of `GetUsersAsync` in the Web project
    - _Requirements: 5.5_

  - [ ]* 7.5 Write property test for UserService search behavior preservation
    - **Property 7: UserService Search Behavior Preservation**
    - **Validates: Requirements 5.3**

  - [ ]* 7.6 Write property test for ApiUserService query string generation
    - **Property 8: ApiUserService Query String Generation**
    - **Validates: Requirements 5.5**

  - [ ]* 7.7 Write property test for UserQueryParams serialization round-trip
    - **Property 1: DTO Serialization Round-Trip (UserQueryParams)**
    - **Validates: Requirements 5.1**

- [ ] 8. Refactor ApiAuthService (Web project) — ResetPasswordAsync and ConfirmEmailAsync
  - [ ] 8.1 Update ApiAuthService.ResetPasswordAsync to accept ResetPasswordRequest DTO
    - Change method to accept a single `ResetPasswordRequest` parameter (reuse existing DTO)
    - Serialize the DTO directly as the JSON body of the POST request
    - Preserve error handling: return `ApiResult.Failure` with response body on non-success status
    - Update all callers in the Web project
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [ ] 8.2 Update ApiAuthService.ConfirmEmailAsync to accept ConfirmEmailRequest DTO
    - Change method to accept a single `ConfirmEmailRequest` parameter (reuse existing DTO)
    - Serialize the DTO directly as the JSON body of the POST request
    - Preserve error handling: return `ApiResult.Failure` with response body on non-success status
    - Update all callers in the Web project
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [ ]* 8.3 Write property test for API client error handling preservation
    - **Property 9: API Client Error Handling Preservation**
    - **Validates: Requirements 6.3, 7.3**

- [ ] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- Existing DTOs (`LoginRequest`, `ResetPasswordRequest`, `ConfirmEmailRequest`) are reused — no new creation needed for those
- All new DTOs follow the `sealed class` convention with XML documentation
- No HTTP wire format changes — controller endpoints remain compatible from the client perspective

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "3.1", "4.1", "6.1", "7.1", "8.1", "8.2"] },
    { "id": 2, "tasks": ["2.2", "3.2", "4.2", "6.2", "7.2", "7.3"] },
    { "id": 3, "tasks": ["2.3", "3.3", "4.3", "6.3", "7.4"] },
    { "id": 4, "tasks": ["2.4", "2.5"] },
    { "id": 5, "tasks": ["2.6", "2.7", "2.8", "3.4", "4.4", "6.4", "6.5", "6.6", "7.5", "7.6", "7.7", "8.3"] }
  ]
}
```
