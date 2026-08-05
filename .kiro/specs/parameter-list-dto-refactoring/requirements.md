# Requirements Document

## Introduction

This feature identifies service and API client methods across the AspireWebAppTemplate solution that have long parameter lists or groups of related parameters suitable for encapsulation into DTOs. The refactoring applies the established `Core/Contracts/{Feature}/` DTO pattern — using `{Action}Request` for mutations and `{Entity}QueryParams` for queries — to improve readability, maintainability, extensibility, and consistency throughout the codebase.

## Glossary

- **DTO**: Data Transfer Object — a class used to encapsulate related parameters into a single strongly-typed object.
- **Request_DTO**: A DTO following the `{Action}Request` naming convention, used for mutation operations (create, update, send).
- **QueryParams_DTO**: A DTO following the `{Entity}QueryParams` naming convention, used for query/search operations.
- **Service_Layer**: The business logic implementations under `ApiService/Services/` that encapsulate all database access and business rules.
- **API_Client_Layer**: The typed HttpClient services under `Web/Services/ApiClients/` that call the ApiService REST endpoints.
- **Contracts_Project**: The `AspireWebAppTemplate.Core/Contracts/` directory structure where all DTOs are organized by feature.
- **Refactoring_Candidate**: A method whose parameter list contains 3+ related parameters that form a cohesive concept suitable for DTO encapsulation.

## Requirements

### Requirement 1: Refactor IEmailService.TrySendEmailAsync to Use a Request DTO

**User Story:** As a developer, I want the `TrySendEmailAsync` method to accept a single request DTO instead of 5 individual parameters, so that the method signature is readable and extensible without breaking changes.

#### Acceptance Criteria

1. WHEN the `TrySendEmailAsync` method is called, THE Email_Service SHALL accept a single `TrySendEmailRequest` parameter containing the following properties with types matching the original parameters: `UserId` (string), `RecipientEmail` (string?), `Category` (NotificationCategory), `EmailType` (EmailType), and `Variables` (Dictionary<string, string>).
2. THE `TrySendEmailRequest` DTO SHALL be a `sealed class` located in the `Core/Contracts/Email/` namespace with XML documentation on the class and all properties.
3. THE Email_Service SHALL preserve all existing `TrySendEmailAsync` behavior after refactoring: user preference checking (EmailEnabled per category), error swallowing (exceptions never propagate to caller), and failure logging.
4. THE API_Client_Layer and all callers of `TrySendEmailAsync` (RegisterService, UserService, LoginService) SHALL be updated to construct and pass the new `TrySendEmailRequest` DTO instead of individual parameters.
5. WHEN the refactoring is complete, THE solution SHALL compile without errors and the original 5-parameter `TrySendEmailAsync` overload SHALL be removed from the `IEmailService` interface and implementation.

### Requirement 2: Refactor ILoginService.ValidateAndGenerateTokenAsync to Use a Request DTO

**User Story:** As a developer, I want the `ValidateAndGenerateTokenAsync` method to accept a single request DTO instead of 4 separate parameters (email, password, rememberMe, returnUrl), so that the signature matches the existing `LoginRequest` pattern already used at the controller layer.

#### Acceptance Criteria

1. WHEN the `ValidateAndGenerateTokenAsync` method is called on `ILoginService`, THE Login_Service SHALL accept a single `LoginRequest` parameter (reusing the existing `Core/Contracts/Auth/LoginRequest` DTO) instead of 4 separate parameters.
2. THE Service_Layer implementation SHALL extract `Email`, `Password`, `RememberMe`, and `ReturnUrl` from the DTO properties.
3. WHEN the refactoring is applied, THE Login_Service SHALL preserve all existing behavior: credential validation, lockout tracking, lockout email notification, 2FA detection, and token generation.
4. THE AuthController caller SHALL pass the already-deserialized `LoginRequest` object directly to the service instead of destructuring it into individual parameters.
5. WHEN `LoginRequest.ReturnUrl` is null, THE Login_Service SHALL treat it as equivalent to the empty or default redirect path, preserving the current null-safe handling.
6. WHEN the refactoring is complete, THE solution SHALL compile without errors and the original 4-parameter signature SHALL be removed from the `ILoginService` interface.

### Requirement 3: Refactor ILdapLoginService.ValidateAndGenerateTokenAsync to Use a Request DTO

**User Story:** As a developer, I want the `ValidateAndGenerateTokenAsync` method on `ILdapLoginService` to accept a single request DTO instead of 4 separate parameters (identifier, password, rememberMe, returnUrl), so that the signature is consistent with the local login service refactoring.

#### Acceptance Criteria

1. WHEN the `ValidateAndGenerateTokenAsync` method is called on `ILdapLoginService`, THE Ldap_Login_Service SHALL accept a single `LoginRequest` parameter (reusing the existing `Core/Contracts/Auth/LoginRequest` DTO) instead of 4 separate parameters.
2. THE Service_Layer implementation SHALL use the `LoginRequest.Email` property as the LDAP identifier (NTID or email), extracting `Password`, `RememberMe`, and `ReturnUrl` from the corresponding DTO properties.
3. WHEN the refactoring is applied, THE Ldap_Login_Service SHALL preserve all existing observable behavior: LDAP bind authentication, auto-provisioning of new local accounts, attribute syncing for existing accounts, and single-use login token generation.
4. THE AuthController caller SHALL pass the `LoginRequest` object directly to the service without destructuring into individual parameters.
5. WHEN `LoginRequest.ReturnUrl` is null, THE Ldap_Login_Service SHALL treat it as equivalent to the empty or default redirect path, preserving the current null-safe handling.
6. WHEN the refactoring is complete, THE solution SHALL compile without errors and the original 4-parameter signature SHALL be removed from the `ILdapLoginService` interface.

### Requirement 4: Refactor IRegisterService.RegisterUserAsync to Use a Request DTO

**User Story:** As a developer, I want the `RegisterUserAsync` method to accept a single request DTO instead of 4 separate parameters (email, password, confirmEmailBaseUri, returnUrl), so that the method signature is cleaner and extensible for future registration fields.

#### Acceptance Criteria

1. WHEN the `RegisterUserAsync` method is called, THE Register_Service SHALL accept a single `RegisterRequest` parameter containing the following properties: `Email` (required, non-empty string), `Password` (required, non-empty string), `ConfirmEmailBaseUri` (required, non-empty string representing the absolute URI to the confirm-email page), and `ReturnUrl` (optional nullable string).
2. THE `RegisterRequest` DTO SHALL be a `sealed class` located in the `Core/Contracts/Auth/` namespace with XML documentation on the class and all properties, following the same pattern as existing request DTOs (e.g., `LoginRequest`, `ChangePasswordRequest`).
3. WHEN `RegisterUserAsync` is called with a valid `RegisterRequest`, THE Register_Service SHALL perform user creation, default role assignment, email confirmation token generation, and welcome email sending, returning the same `Task<RegisterResult>` as the previous signature.
4. THE AuthController `Register` endpoint SHALL construct a `RegisterRequest` instance from the incoming HTTP request data and pass it to `RegisterUserAsync`, with no remaining references to the old multi-parameter method signature in the solution.
5. IF `RegisterRequest` is passed with a null or empty `Email` or `Password`, THEN THE Register_Service SHALL return a failed `RegisterResult` without creating a user account.

### Requirement 5: Refactor IUserService.SearchAsync to Use a QueryParams DTO

**User Story:** As a developer, I want the `SearchAsync` method on `IUserService` to accept a single query params DTO instead of 3 individual nullable parameters (page, pageSize, searchTerm), so that the method signature follows the established `{Entity}QueryParams` pattern used by `AuditLogQueryParams`, `NotificationQueryParams`, and `AnnouncementQueryParams`.

#### Acceptance Criteria

1. WHEN the `SearchAsync` method is called, THE User_Service SHALL accept a single `UserQueryParams` parameter containing `Page` (int, default 0), `PageSize` (int, default 10), and `SearchTerm` (string?, default null) properties.
2. THE `UserQueryParams` DTO SHALL be a `sealed class` located in the `Core/Contracts/Users/` namespace, with property defaults matching the current behavior: `Page = 0` (zero-based), `PageSize = 10`, and `SearchTerm = null`.
3. WHEN the refactoring is applied, THE User_Service SHALL preserve all existing behavior: case-insensitive partial search term matching against username, display name, email, first name, last name, and department fields; pagination using the page and pageSize values; and result ordering by display name ascending.
4. THE UsersController SHALL bind query string parameters to the `UserQueryParams` DTO using `[FromQuery]` and pass the populated DTO to the service method.
5. THE API_Client_Layer (`ApiUserService.GetUsersAsync`) SHALL accept a `UserQueryParams` parameter and serialize its properties as query string parameters in the HTTP GET request to `/api/users`.

### Requirement 6: Refactor ApiAuthService.ResetPasswordAsync to Use the Existing ResetPasswordRequest DTO

**User Story:** As a developer, I want the `ResetPasswordAsync` method in `ApiAuthService` to accept the existing `ResetPasswordRequest` DTO instead of 3 separate string parameters (email, code, newPassword), so that the Web-tier API client follows the same DTO convention used at the controller layer.

#### Acceptance Criteria

1. WHEN the `ResetPasswordAsync` method is called on `ApiAuthService`, THE API_Client_Layer SHALL accept a single `ResetPasswordRequest` parameter (reusing the existing `Core/Contracts/Auth/ResetPasswordRequest` DTO) instead of 3 separate string parameters.
2. WHEN `ResetPasswordAsync` sends the HTTP request, THE API_Client_Layer SHALL serialize the `ResetPasswordRequest` DTO directly as the JSON body of the POST request to `/api/auth/reset-password`.
3. IF the API returns a non-success HTTP status code, THEN THE API_Client_Layer SHALL return an `ApiResult.Failure` containing the response body as the error message, preserving the existing error-handling behavior.
4. ALL callers of `ApiAuthService.ResetPasswordAsync` in the Web project SHALL be updated to construct and pass the `ResetPasswordRequest` DTO, and the solution SHALL compile without errors after the refactoring.

### Requirement 7: Refactor ApiAuthService.ConfirmEmailAsync to Use the Existing ConfirmEmailRequest DTO

**User Story:** As a developer, I want the `ConfirmEmailAsync` method in `ApiAuthService` to accept the existing `ConfirmEmailRequest` DTO instead of 2 separate string parameters (userId, code), so that the Web-tier API client follows the same DTO convention used at the controller layer.

#### Acceptance Criteria

1. WHEN the `ConfirmEmailAsync` method is called on `ApiAuthService`, THE API_Client_Layer SHALL accept a single `ConfirmEmailRequest` parameter (reusing the existing `Core/Contracts/Auth/ConfirmEmailRequest` DTO) instead of 2 separate string parameters.
2. WHEN `ConfirmEmailAsync` sends the HTTP request, THE API_Client_Layer SHALL serialize the `ConfirmEmailRequest` DTO directly as the JSON body of the POST request to `/api/auth/confirm-email`.
3. IF the API returns a non-success HTTP status code, THEN THE API_Client_Layer SHALL return an `ApiResult.Failure` containing the response body as the error message, preserving the existing error-handling behavior.
4. ALL callers of `ApiAuthService.ConfirmEmailAsync` in the Web project SHALL be updated to construct and pass the `ConfirmEmailRequest` DTO, and the solution SHALL compile without errors after the refactoring.

### Requirement 8: Refactor IEmailService.SendEmailAsync to Use a Request DTO

**User Story:** As a developer, I want the `SendEmailAsync` method to accept a single request DTO instead of 3 parameters (emailType, recipientEmail, variables), so that the method signature is consistent with the `TrySendEmailAsync` refactoring and extensible for future fields such as CC recipients or attachments.

#### Acceptance Criteria

1. WHEN the `SendEmailAsync` method is called, THE Email_Service SHALL accept a single `SendEmailRequest` parameter containing an `EmailType` property, a `RecipientEmail` string property, and a `Variables` dictionary property (Dictionary<string, string>).
2. THE `SendEmailRequest` DTO SHALL be a `sealed class` located in `Core/Contracts/Email/` namespace with XML documentation (`<summary>`) on the class and all public properties.
3. WHEN the refactored `SendEmailAsync` is invoked, THE Email_Service SHALL resolve templates via `IEmailTemplateService.RenderAsync`, send via SMTP, fall back to no-op logging when SMTP is not configured, throw `InvalidOperationException` on SMTP failure, and throw `KeyNotFoundException` when no active template exists for the specified EmailType — identical to the pre-refactor behavior.
4. ALL internal callers of `SendEmailAsync` within `EmailService` (Identity integration methods and `TrySendEmailAsync`) SHALL be updated to construct and pass a `SendEmailRequest` instance instead of individual parameters.
5. IF any existing test references the old 3-parameter `SendEmailAsync` signature, THEN THE test code SHALL be updated to use the new `SendEmailRequest` DTO so that all tests compile and pass.
