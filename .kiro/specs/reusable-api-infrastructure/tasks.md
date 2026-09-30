# Implementation Plan: Reusable API Infrastructure

## Overview

This is a behavior-preserving reusability refactor of the `AspireWebAppTemplate` solution. It introduces three reusable seams plus three additive exception mappings, then mechanically removes the boilerplate they replace.

The plan is staged so the **central, additive pieces land first** (they coexist with existing code and change no behavior), then the **mechanical cleanups happen incrementally** — controller-by-controller and client-method-by-method — with a build + full test run after each step so the solution stays green throughout.

Ordering guarantees:

- The `ExceptionMappingHandler` is added and registered **before** any controller try/catch is removed (the handler observes nothing until the redundant catches are gone, so behavior is unchanged while both coexist).
- `HttpResponseMessageExtensions` and `QueryStringBuilder` are added **before** any typed client is migrated.
- `AddApiClient<TClient>` is introduced **before** the registration block is converted.

Implementation language: **C#** (the design specifies concrete C# throughout — no pseudocode). New tests live in the existing `AspireWebAppTemplate.Tests` project under `ControllerServiceRefactor/` (extending the existing `ExceptionMappingTests.cs` neighborhood) and a new `ReusableApiInfrastructure/` folder for the Web-helper tests.

## Tasks

- [x] 1. Add the Central_Exception_Handler (additive, coexists with existing try/catch)
  - [x] 1.1 Create `ExceptionMappingHandler` implementing `IExceptionHandler`
    - Create `AspireWebAppTemplate.ApiService/Exceptions/ExceptionMappingHandler.cs`
    - Traditional constructor injecting `IProblemDetailsService` (no primary constructor); `#region Constructor` and `#region Exception Handling`; XML `<summary>` on the class, the private field, the constructor, and `TryHandleAsync`; present-tense comments only
    - Implement `TryHandleAsync` with a single `switch` mapping the seven types: `KeyNotFoundException` → 404, `InvalidOperationException` → 400, `ArgumentException` (incl. subtypes) → 400, `UnauthorizedAccessException` → 403, `NotImplementedException` → 501, `TimeoutException` → 504; write `exception.Message` into `ProblemDetails.Detail`; return `false` for any other type so the default handler produces a 500 with no detail
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 1.10, 5.1, 5.2, 5.3, 5.4_

  - [x] 1.2 Register the handler in `Program.cs`
    - Add `builder.Services.AddExceptionHandler<ExceptionMappingHandler>();` next to the existing `AddProblemDetails()`
    - Do NOT add `AddProblemDetails()` or `UseExceptionHandler()` — both are already present
    - _Requirements: 1.1_

  - [x] 1.3 Write property test for mapped-exception status + message (Property 1)
    - Place in `Tests/ControllerServiceRefactor/` (e.g., `ExceptionMappingHandlerTests.cs`)
    - **Property 1: Mapped exceptions produce the correct status and preserve the message**
    - Generate the seven mapped types (incl. `ArgumentNullException` / `ArgumentOutOfRangeException`) with arbitrary messages; drive `TryHandleAsync` with a test `HttpContext`; assert status (404/400/403/501/504) and that the written body's message field equals the input message
    - `[Property(MaxTest = 2)]`; tag `// Feature: reusable-api-infrastructure, Property 1: Mapped exceptions produce the correct status and preserve the message`
    - **Validates: Requirements 1.2, 1.3, 1.4, 1.5, 4.1, 4.2, 4.5**

  - [x] 1.4 Write property test for unmapped-exception passthrough (Property 2)
    - **Property 2: Unmapped exceptions are left to the default handler**
    - Generate exception types outside the seven mapped types; assert `TryHandleAsync` returns `false` and writes no message
    - `[Property(MaxTest = 2)]`; tag `// Feature: reusable-api-infrastructure, Property 2: Unmapped exceptions are left to the default handler`
    - **Validates: Requirements 1.6, 4.6**

  - [x] 1.5 Write integration test for the wired handler over real HTTP
    - Use `Aspire.Hosting.Testing` / `WebApplicationFactory<Program>` against ApiService (mirror `WebTests.cs`)
    - Assert an endpoint whose service throws `KeyNotFoundException` → 404 (message in body), `InvalidOperationException` → 400, `ArgumentException` → 400; and an unmapped exception → 500 with no detail
    - Validates the wiring (`AddExceptionHandler` + `UseExceptionHandler` + `AddProblemDetails`)
    - _Requirements: 1.1, 4.1, 4.2, 4.4, 4.6_

- [x] 2. Checkpoint - handler added, nothing removed yet
  - Build the full solution and run the entire existing test suite; behavior must be unchanged (handler coexists with inline try/catch). Ensure all tests pass, ask the user if questions arise.

- [x] 3. Add the Web helpers (additive; no client changed yet)
  - [x] 3.1 Create `HttpResponseMessageExtensions` (`ToApiResultAsync<T>` / `ToApiResultAsync`)
    - Create `AspireWebAppTemplate.Web/Services/ApiClients/HttpResponseMessageExtensions.cs`
    - `ToApiResultAsync<T>(T? defaultValue = default, JsonSerializerOptions? options = null)`: on success deserialize and return `ApiResult<T>.Success(data ?? defaultValue!)`; on failure return `ApiResult<T>.Failure(await ReadAsStringAsync())`
    - `ToApiResultAsync()`: success → `ApiResult.Success()`; failure → `ApiResult.Failure(await ReadAsStringAsync())`
    - Exactly one body read per branch (no extra requests/retries/reads); XML `<summary>` on the class and both methods; present-tense comments
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.8, 5.1, 5.2, 5.3, 5.5, 5.9_

  - [x] 3.2 Write property test for response mapping (Property 3)
    - Place in a new `Tests/ReusableApiInfrastructure/` folder (e.g., `HttpResponseMappingTests.cs`)
    - **Property 3: Response mapping matches the current inline branch**
    - Generate success/failure statuses and reference/value/null payloads plus arbitrary failure bodies; build an `HttpResponseMessage`; assert `(Succeeded, Error, Data)` equals a reference implementation mirroring the current inline branch (incl. null-default fallback and `ApiResult<int>`)
    - `[Property(MaxTest = 2)]`; tag `// Feature: reusable-api-infrastructure, Property 3: Response mapping matches the current inline branch`
    - **Validates: Requirements 2.2, 2.3, 2.4, 2.5, 2.6, 2.8, 4.7**

  - [x] 3.3 Write unit test for single body-read structure
    - Assert the helper performs exactly one body read per branch (success and failure)
    - _Requirements: 2.8_

  - [x] 3.4 Create `QueryStringBuilder`
    - Create `AspireWebAppTemplate.Web/Services/ApiClients/QueryStringBuilder.cs`
    - `static Build(string path, Action<QueryStringBuilder> configure)`, instance `Add`, `AddIfHasValue<T>` (struct), `AddIfNotWhiteSpace` (escapes via `Uri.EscapeDataString`); `?` emitted only when ≥1 part; parts joined with `&`
    - Match today's escaping/omission byte-for-byte; XML `<summary>` on class, private field, and every method; present-tense comments
    - _Requirements: 2.9, 5.1, 5.2, 5.3, 5.5_

  - [x] 3.5 Write property test for query-string equivalence (Property 4)
    - Place in `Tests/ReusableApiInfrastructure/` (e.g., `QueryStringBuilderTests.cs`)
    - **Property 4: Query-string builder output equals the current hand-built string**
    - Generate `UserQueryParams` / `NotificationQueryParams` / `AnnouncementQueryParams` combinations (present/absent optionals, escape-worthy search terms); assert output equals a reference builder reproducing today's interpolation for the users, notifications, and announcements shapes
    - `[Property(MaxTest = 2)]`; tag `// Feature: reusable-api-infrastructure, Property 4: Query-string builder output equals the current hand-built string`
    - **Validates: Requirements 2.9**

- [x] 4. Introduce the Client_Registration_Helper and convert Template registrations
  - [x] 4.1 Add `AddApiClient<TClient>` and convert the Template block to one-liners
    - In `AspireWebAppTemplate.Web/Extensions/ApiClientServiceExtensions.cs` add the local `private static IServiceCollection AddApiClient<TClient>(this IServiceCollection services) where TClient : class` that calls `AddHttpClient<TClient>(...BaseAddress = ApiServiceBaseAddress).AddHttpMessageHandler<UserIdentityDelegatingHandler>()`
    - Convert the ~10 `#region Template` registrations (`ApiWeatherService`, `ApiAuthService`, `ApiUserService`, `ApiRoleService`, `ApiAuditLogService`, `ApiPagePermissionService`, `ApiNotificationService`, `ApiNavigationService`, `ApiAnnouncementService`, `ApiEmailTemplateService`) to `services.AddApiClient<...>()`
    - Keep the `ApiServiceBaseAddress` const and both `#region Template` / `#region Business` seams; update the `#region Business` example comment to the one-line form; XML `<summary>` on the helper
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 5.1, 5.2, 5.5_

  - [x] 4.2 Write unit test resolving each registered client
    - Build the provider; resolve each of the ten template clients and assert each is a typed `HttpClient` with `BaseAddress == ApiServiceBaseAddress` and `UserIdentityDelegatingHandler` in its pipeline; assert no unexpected client is registered
    - _Requirements: 3.2, 3.3_

- [x] 5. Checkpoint - all seams added, no behavior touched
  - Build the full solution and run the entire existing test suite. Ensure all tests pass, ask the user if questions arise.

- [x] 6. Remove redundant controller try/catch (group A)
  - [x] 6.1 Remove bare triad catches from `RolesController` and `UsersController`
    - Delete only `catch (KeyNotFoundException)` / `catch (InvalidOperationException)` / `catch (ArgumentException)` clauses that merely return `NotFound(ex.Message)` / `BadRequest(ex.Message)`; leave subset-only opt-out catches that need different behavior in place
    - Keep every `[ProducesResponseType]` attribute and `<response>` XML doc unchanged
    - Build + run tests after this file group
    - _Requirements: 1.7, 1.8, 1.11, 1.12, 1.13, 4.1, 4.2, 4.3, 5.10_

  - [x] 6.2 Remove bare triad catches from `AuthController` and `EmailTemplateController`
    - Same removal rule; preserve `[ProducesResponseType]` attributes and `<response>` docs
    - Build + run tests after this file group
    - _Requirements: 1.7, 1.8, 1.11, 1.12, 1.13, 4.1, 4.2, 4.3, 5.10_

- [x] 7. Remove redundant controller try/catch (group B)
  - [x] 7.1 Remove bare triad catches from `NotificationController` and `AnnouncementController`
    - Same removal rule; preserve `[ProducesResponseType]` attributes and `<response>` docs
    - Build + run tests after this file group
    - _Requirements: 1.7, 1.8, 1.11, 1.12, 1.13, 4.1, 4.2, 4.3, 5.10_

  - [x] 7.2 Remove bare triad catches from `AuditLogController`
    - Same removal rule; preserve `[ProducesResponseType]` attributes and `<response>` docs
    - Build + run tests after this file
    - _Requirements: 1.7, 1.8, 1.11, 1.12, 1.13, 4.1, 4.2, 4.3, 5.10_

  - [x] 7.3 Remove bare triad catches from `PagePermissionsController` (opt-out preserved)
    - Remove only the three bare triad catches from `UpdateRolePermissions`; KEEP the `if (role is null) return NotFound(...)` guard clause and the audit write (Opt_Out_Action non-exception behavior)
    - Preserve `[ProducesResponseType]` attributes and `<response>` docs
    - Build + run tests after this file
    - _Requirements: 1.7, 1.8, 1.11, 1.12, 1.13, 4.1, 4.2, 4.3, 5.10, 5.11_

- [x] 8. Checkpoint - controller cleanup complete
  - Build the full solution and run the entire existing test suite; every mapped exception now propagates to the central handler with identical status/message. Ensure all tests pass, ask the user if questions arise.

- [x] 9. Migrate typed clients to the helpers (group A)
  - [x] 9.1 Migrate `ApiUserService` (excluding the streaming method)
    - Pipe request/response methods through `ToApiResultAsync` / `ToApiResultAsync<T>`, preserving each method's exact null-handling (`?? []` / `?? new()` → `defaultValue`; `!` → no `defaultValue`)
    - Use `QueryStringBuilder` in `GetUsersAsync` and `GetAllUsersAsync`
    - Explicitly DO NOT touch `SyncLdapUsersStreamAsync`
    - Build + run tests after this file
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9, 4.1, 4.3, 5.10_

  - [x] 9.2 Migrate `ApiRoleService` and `ApiAuthService`
    - Pipe through `ToApiResultAsync` overloads, preserving each method's null-handling
    - Build + run tests after this file group
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.8, 4.1, 4.3, 5.10_

  - [x] 9.3 Migrate `ApiNotificationService` (incl. query string + value-type payload)
    - Use `QueryStringBuilder` in `GetNotificationsAsync` (page/pageSize always present, optional category/isRead); keep `GetUnreadCountAsync` as `ApiResult<int>` via `ToApiResultAsync<int>()`
    - Build + run tests after this file
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.8, 2.9, 4.1, 4.3, 5.10_

- [x] 10. Migrate typed clients to the helpers (group B)
  - [x] 10.1 Migrate `ApiAnnouncementService` (incl. optional query string)
    - Pipe through `ToApiResultAsync` overloads; use `QueryStringBuilder` in `GetForListPageAsync` if applicable; preserve `?? new()` via `defaultValue: new()`
    - Build + run tests after this file
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.8, 2.9, 4.1, 4.3, 5.10_

  - [x] 10.2 Migrate `ApiPagePermissionService` and `ApiEmailTemplateService`
    - Pipe through `ToApiResultAsync` overloads, preserving each method's null-handling
    - Build + run tests after this file group
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.8, 4.1, 4.3, 5.10_

  - [x] 10.3 Migrate `ApiAuditLogService`, `ApiNavigationService`, and `ApiWeatherService`
    - Pipe through `ToApiResultAsync` overloads, preserving each method's null-handling
    - Build + run tests after this file group
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.8, 4.1, 4.3, 5.10_

  - [x] 10.4 Use `QueryStringBuilder` in `ApiAuditLogService` query strings
    - `GetPagedAsync`: replace the hand-built `/api/audit-log?page=..&pageSize=..` string with `QueryStringBuilder.Build` (page/pageSize always via `Add`; searchTerm via `AddIfNotWhiteSpace`; actionType/entityType/dateStart/dateEnd via `AddIfHasValue`; sortBy via `AddIfNotWhiteSpace`; emit `sortDescending=false` only when `!SortDescending`). Preserve the exact same emitted parameters and formats (`:O` for dates, enum default string form) and the `ToApiResultAsync<PagedResult<AuditLogEntryDto>>()` mapping
    - `ExportExcelAsync`: replace the hand-built `/api/audit-log/export?` string with `QueryStringBuilder.Build` (searchTerm via `AddIfNotWhiteSpace`; actionType/entityType/dateStart/dateEnd via `AddIfHasValue`). This also removes the pre-existing `?&`/bare-`?` quirk of the manual string; keep `ExportExcelAsync`'s byte-reading success/failure branch unchanged (non-JSON, different shape)
    - Build + run tests after this file
    - _Requirements: 2.9, 5.1, 5.2, 5.3, 5.5, 5.10_


  - [x] 10.5 Relocate `HttpResponseMessageExtensions` and `QueryStringBuilder` to their convention folders
    - Move `Services/ApiClients/HttpResponseMessageExtensions.cs` to `Extensions/` and change its namespace to `AspireWebAppTemplate.Web.Extensions` (the folder already holds `ApiClientServiceExtensions`, `ApplicationServiceExtensions`, `HttpClientCertificateExtensions`)
    - Move `Services/ApiClients/QueryStringBuilder.cs` to `Utilities/` and change its namespace to `AspireWebAppTemplate.Web.Utilities` (pure helper with no HTTP/DI dependency; mirrors `Application/Utilities` and `UI/Utilities`)
    - Update consumer `using` directives: add `using AspireWebAppTemplate.Web.Extensions;` to the ten `Api*Service.cs` files and the two response-mapping test files (`HttpResponseMappingTests`, `HttpResponseMappingBodyReadTests`); add `using AspireWebAppTemplate.Web.Utilities;` to the four clients that use the builder (`ApiUserService`, `ApiNotificationService`, `ApiAuditLogService`, `ApiAnnouncementService`) and `QueryStringBuilderTests`
    - Keep `using AspireWebAppTemplate.Web.Services.ApiClients;` where it is still needed for `ApiNavigationService` (`NavMenu.razor.cs`, `NavigationServiceUnitTests`, `ApiClientServiceExtensions`, `ApiClientRegistrationTests`)
    - Behavior-preserving move only: no type members, logic, or the `ApiServiceBaseAddress` constant change. Build the full solution + run the entire test suite
    - _Requirements: 5.1, 5.2, 5.10_
- [x] 11. Final checkpoint - full build + full suite green
  - Build the entire solution and run the complete existing test suite (Announcements, AuditLog, ControllerServiceRefactor, Email, Navigation, Notifications, PagePermissions, StatusAlert, Scheduler, WebTests, plus the new ReusableApiInfrastructure tests); confirm zero failed and zero skipped tests attributable to the refactor. Clean up any temporary artifacts. Ensure all tests pass, ask the user if questions arise.
  - _Requirements: 4.4_

## Notes

- Tasks marked with `*` are optional test sub-tasks and can be skipped for a faster MVP; core implementation tasks are never optional.
- Each task references specific requirement sub-clauses for traceability.
- Ordering keeps the build green at every step: the exception handler is registered (tasks 1–2) before any controller catch is removed (tasks 6–7); the Web helpers exist (task 3) and `AddApiClient` exists (task 4) before any client is migrated (tasks 9–10).
- Property tests validate the four universal correctness properties; unit and integration tests validate wiring, opt-out behavior, `[ProducesResponseType]` preservation, and client registration.
- `SyncLdapUsersStreamAsync` is never migrated (Requirement 2.7). No DTO, route, or `ApiResult` contract is changed (Requirements 4.3, 5.9). No new project is created (Requirements 5.7, 5.8).

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "3.1", "3.4"] },
    { "id": 1, "tasks": ["1.2", "1.3", "1.4", "3.2", "3.3", "3.5", "4.1"] },
    { "id": 2, "tasks": ["1.5", "4.2", "6.1", "6.2"] },
    { "id": 3, "tasks": ["7.1", "7.2", "7.3"] },
    { "id": 4, "tasks": ["9.1", "9.2", "9.3"] },
    { "id": 5, "tasks": ["10.1", "10.2", "10.3"] }
  ]
}
```
