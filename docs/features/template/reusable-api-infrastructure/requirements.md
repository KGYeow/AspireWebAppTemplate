# Requirements Document

## Introduction

This feature is a reusability refactor of the `AspireWebAppTemplate` solution. A full-codebase reusability audit identified three high-value "reuse now" patterns that every future business feature would otherwise re-incur as boilerplate. This refactor extracts those three patterns into small, in-project reusable seams so that new controllers and API clients start from the happy path instead of copy-pasting error/mapping/registration ceremony.

Because this is a template project, the guiding intent is to reduce boilerplate **without** introducing premature abstraction, **without** removing or degrading any existing template capability (per `template-guardrails` steering), and **without** changing external behavior. The three items are:

1. **Centralized API exception-to-HTTP-status mapping** in the `AspireWebAppTemplate.ApiService` project.
2. **API-client HTTP-result mapping helper(s)** in the `AspireWebAppTemplate.Web` project.
3. **Typed-client registration helper** in the `AspireWebAppTemplate.Web` project.

The refactor is strictly behavior-preserving: HTTP status codes, response body error text, and Web-side `ApiResult`/`ApiResult<T>` semantics remain equivalent to the current implementation. Cross-cutting concerns cover regression safety, coding standards, layering, and explicit non-goals.

### Current-state grounding (verified in the codebase)

- Nearly every controller action in `ApiService/Controllers/*` wraps its service call in a try/catch mapping the triad: `KeyNotFoundException` → `404 NotFound(ex.Message)`, `InvalidOperationException` → `400 BadRequest(ex.Message)`, `ArgumentException` → `400 BadRequest(ex.Message)`. This is duplicated across `RolesController`, `UsersController`, `AuthController` (~20 copies), `EmailTemplateController`, `NotificationController`, `AnnouncementController`, `PagePermissionsController`, and `AuditLogController`. Some actions intentionally catch only a subset (e.g., `RolesController.GetRole`/`GetUsersInRole` catch only `KeyNotFoundException`).
- `ApiService/Program.cs` already calls `builder.Services.AddProblemDetails()` and `app.UseExceptionHandler()`, so the central-handler seam can be added with minimal wiring.
- Every method in `Web/Services/ApiClients/*` repeats: call endpoint → check `response.IsSuccessStatusCode` → on success deserialize + return `ApiResult<T>.Success(...)` (or `ApiResult.Success()`), on failure return `ApiResult.Failure(await response.Content.ReadAsStringAsync())`. This appears ~60+ times across ~10 typed clients. Null-handling varies per method (`?? []`, `?? new()`, `!` non-null assertion, `GetUnreadCountAsync` returns `ApiResult<int>`).
- `ApiUserService.SyncLdapUsersStreamAsync` is a streaming (`IAsyncEnumerable`, NDJSON) method that does not fit the request/response mapping shape.
- `ApiUserService` and `ApiNotificationService` build query strings by hand from optional parameters.
- `Web/Extensions/ApiClientServiceExtensions.cs` has ~10 near-identical `AddHttpClient<TClient>(...).AddHttpMessageHandler<UserIdentityDelegatingHandler>()` blocks, split by `#region Template` and `#region Business`, using a `private const string ApiServiceBaseAddress`.

## Glossary

- **ApiService**: The `AspireWebAppTemplate.ApiService` project — the REST API host containing thin controllers and `Program.cs`.
- **Web_Project**: The `AspireWebAppTemplate.Web` Blazor Server project containing typed API-client services and DI registration extensions.
- **Exception_Mapping_Triad**: The current mapping of `KeyNotFoundException` → 404, `InvalidOperationException` → 400, `ArgumentException` → 400, where the response body is the exception's `Message`.
- **Central_Exception_Handler**: The centralized ASP.NET Core exception-handling mechanism (an `IExceptionHandler` implementation producing `ProblemDetails`) registered in `ApiService/Program.cs` that applies the Exception_Mapping_Triad.
- **Api_Client_Mapping_Helper**: The reusable Web_Project seam (a base class `ApiClientBase` and/or `HttpResponseMessage`/`HttpClient` extension methods such as `ToApiResultAsync<T>()` and `ToApiResultAsync()`) that converts an HTTP response into an `ApiResult` or `ApiResult<T>`.
- **Query_String_Helper**: An optional small Web_Project helper that builds a query string from optional parameters (used by `ApiUserService` and `ApiNotificationService`).
- **Client_Registration_Helper**: A local private helper (e.g., `AddApiClient<TClient>`) in `ApiClientServiceExtensions.cs` that encapsulates the repeated typed-client registration block.
- **ApiResult / ApiResult&lt;T&gt;**: The Web-side result wrapper types in `Application/Common/ApiResult.cs` with `Succeeded`, `Error`, and (generic) `Data` members and `Success`/`Failure` factory methods.
- **ProblemDetails**: The RFC 7807 problem-details response model produced by ASP.NET Core exception handling.
- **Opt_Out_Action**: A controller action that intentionally performs its own custom exception handling and therefore is not governed by the Central_Exception_Handler.

## Requirements

### Requirement 1: Centralized API exception-to-HTTP-status mapping

**User Story:** As a template developer writing a new API controller, I want unhandled service exceptions to be mapped to HTTP status codes centrally, so that my controller actions can contain only the happy path without repeating the try/catch Exception_Mapping_Triad. In addition to the existing Exception_Mapping_Triad, the Central_Exception_Handler ships three additive default mappings — `UnauthorizedAccessException` → 403, `NotImplementedException` → 501, and `TimeoutException` → 504 — that give future business features sensible defaults. These three exception types are not thrown anywhere in the existing codebase, so the additional mappings do not alter any current controller behavior and only define behavior for cases that cannot occur today.

#### Acceptance Criteria

1. THE ApiService SHALL register a Central_Exception_Handler in `ApiService/Program.cs` using the ASP.NET Core `IExceptionHandler` and `ProblemDetails` mechanism.
2. WHEN a controller action allows a `KeyNotFoundException` to propagate, THE Central_Exception_Handler SHALL produce an HTTP `404 Not Found` response.
3. WHEN a controller action allows an `InvalidOperationException` to propagate, THE Central_Exception_Handler SHALL produce an HTTP `400 Bad Request` response.
4. WHEN a controller action allows an `ArgumentException` or any exception deriving from `ArgumentException` (including `ArgumentNullException` and `ArgumentOutOfRangeException`) to propagate, THE Central_Exception_Handler SHALL produce an HTTP `400 Bad Request` response.
5. WHEN a controller action allows an `UnauthorizedAccessException` to propagate, THE Central_Exception_Handler SHALL produce an HTTP `403 Forbidden` response.
6. WHEN a controller action allows a `NotImplementedException` to propagate, THE Central_Exception_Handler SHALL produce an HTTP `501 Not Implemented` response.
7. WHEN a controller action allows a `TimeoutException` to propagate, THE Central_Exception_Handler SHALL produce an HTTP `504 Gateway Timeout` response.
8. WHEN the Central_Exception_Handler produces a `404`, `400`, `403`, `501`, or `504` response for a mapped exception, THE Central_Exception_Handler SHALL include the exception's `Message` text unmodified in the response body in the same manner as the mapped `404`/`400` responses, so that the response body text is character-for-character equivalent to the text a corresponding inline `NotFound(ex.Message)` / `BadRequest(ex.Message)` / `StatusCode(status, ex.Message)` call would return.
9. IF a propagated exception is not `KeyNotFoundException`, `InvalidOperationException`, `ArgumentException`, a subtype of `ArgumentException`, `UnauthorizedAccessException`, `NotImplementedException`, or `TimeoutException`, THEN THE Central_Exception_Handler SHALL produce an HTTP `500 Internal Server Error` response whose body does NOT include the exception's `Message` text, preserving the current behavior for unmapped exceptions.
10. THE Central_Exception_Handler SHALL define its exception-to-status mappings in a single mapping location (for example, one `switch`) so that an application author can add an additional exception-to-status mapping by extending that single location without modifying the mapping mechanism's structure.
11. WHERE a controller action is designated an Opt_Out_Action by retaining its own try/catch, THE Central_Exception_Handler SHALL produce a response only for exceptions that propagate past the action, and SHALL NOT alter or override any response the action itself returns for exceptions it handles.
12. THE ApiService SHALL retain every existing `[ProducesResponseType]` attribute on controller actions, unchanged in status code and count, so that OpenAPI documentation advertises the identical set of response codes as before this change.
13. WHEN a controller action's mapped exceptions (from the Exception_Mapping_Triad) are handled by the Central_Exception_Handler, THE ApiService SHALL remove the corresponding redundant inline try/catch blocks from that action so that the action's remaining body contains no `catch` clause for `KeyNotFoundException`, `InvalidOperationException`, or `ArgumentException`.

### Requirement 2: API-client HTTP-result mapping helper

**User Story:** As a template developer writing a new typed API client in the Web_Project, I want a reusable helper that converts an HTTP response into an `ApiResult`, so that each client method reduces to the endpoint call plus a mapping expression instead of repeating the success/failure branch.

#### Acceptance Criteria

1. THE Web_Project SHALL provide an Api_Client_Mapping_Helper implemented as a base class (`ApiClientBase`) and/or `HttpResponseMessage`/`HttpClient` extension methods (`ToApiResultAsync<T>()` and `ToApiResultAsync()`), located within the Web_Project.
2. WHEN an HTTP response has a success status code (status code 200-299) and a value-returning mapping is requested, THE Api_Client_Mapping_Helper SHALL deserialize the response body to the requested type and return an `ApiResult<T>` whose `Succeeded` is true and whose `Data` equals the deserialized value.
3. WHEN an HTTP response has a success status code (status code 200-299) and a no-data mapping is requested, THE Api_Client_Mapping_Helper SHALL return an `ApiResult` whose `Succeeded` is true and whose `Error` is null.
4. IF an HTTP response does not have a success status code (status code outside 200-299), THEN THE Api_Client_Mapping_Helper SHALL read the entire response body as a string and return a failure result (`ApiResult.Failure(body)` or `ApiResult<T>.Failure(body)` for value-returning mappings) whose `Succeeded` is false and whose `Error` is byte-for-byte identical to the string the current per-method code produces from `HttpContent.ReadAsStringAsync` for the same response.
5. WHEN a value-returning mapping is requested, a success status code (200-299) is returned, the deserialized payload is null, and the calling method supplies a fallback default (for example `?? []` or `?? new()`), THE Api_Client_Mapping_Helper SHALL return an `ApiResult<T>` whose `Data` equals that supplied default value rather than null.
6. WHERE a client method returns a value-type payload (for example `GetUnreadCountAsync` returning `ApiResult<int>`), THE Api_Client_Mapping_Helper SHALL return an `ApiResult<T>` for that value type with `Data` equal to the deserialized value on success.
7. THE Web_Project SHALL leave `ApiUserService.SyncLdapUsersStreamAsync` source unchanged so that the NDJSON streaming behavior (per-line deserialization and yielded progress items) is preserved.
8. THE Api_Client_Mapping_Helper SHALL produce results in which the `Succeeded`, `Error`, and `Data` values are identical to those produced by the current per-method success/failure branches for the same HTTP response, introducing no additional HTTP requests, retries, or body reads beyond those the current code performs.
9. WHERE the Query_String_Helper is introduced, THE Web_Project SHALL, for any given set of input parameter values, produce a query string byte-for-byte identical to the current hand-built query strings in `ApiUserService` and `ApiNotificationService`, including URL-escaping of parameter values via the same escaping applied today and omission of parameters whose optional value is unset (null, or empty/whitespace where the current code omits them).

### Requirement 3: Typed-client registration helper

**User Story:** As a template developer registering a new typed API client, I want a single local helper that encapsulates the repeated registration block, so that adding a client is a one-line call instead of a copied multi-line block.

#### Acceptance Criteria

1. THE Web_Project SHALL provide a Client_Registration_Helper as a local private generic method (for example `AddApiClient<TClient>`) within `ApiClientServiceExtensions.cs` that accepts the `IServiceCollection` and returns the `IServiceCollection` so that a client is registered in a single method call.
2. WHEN the Client_Registration_Helper registers a typed client `TClient`, THE Client_Registration_Helper SHALL register `TClient` as a typed `HttpClient`, set the client's `BaseAddress` from the `ApiServiceBaseAddress` constant, and attach the `UserIdentityDelegatingHandler` message handler.
3. THE Web_Project SHALL register each of the following typed clients through the Client_Registration_Helper exactly once — `ApiWeatherService`, `ApiAuthService`, `ApiUserService`, `ApiRoleService`, `ApiAuditLogService`, `ApiPagePermissionService`, `ApiNotificationService`, `ApiNavigationService`, `ApiAnnouncementService`, `ApiEmailTemplateService` — so that each client resolves as a typed `HttpClient` with `BaseAddress` equal to the `ApiServiceBaseAddress` value and the `UserIdentityDelegatingHandler` attached, and no other typed client is registered.
4. THE Web_Project SHALL retain the `#region Template` and `#region Business` seams in `ApiClientServiceExtensions.cs`.
5. THE Web_Project SHALL retain the `ApiServiceBaseAddress` constant as the single source of the service-discovery base address, referenced by the Client_Registration_Helper.
6. WHERE an application author adds an application-specific typed client, THE Web_Project SHALL allow that client to be registered within the `#region Business` seam through the Client_Registration_Helper using a single method call, without modifying the `#region Template` seam.

### Requirement 4: Behavior preservation and regression safety

**User Story:** As a template maintainer, I want the refactor to be provably behavior-preserving, so that existing consumers, tests, and OpenAPI documentation continue to work without change.

#### Acceptance Criteria

1. THE refactor SHALL preserve, for every controller action, the identical HTTP status code returned for each equivalent request and outcome (success, not-found, and validation-failure) as produced before the refactor.
2. THE refactor SHALL preserve the response-body error text for mapped exceptions such that the text is byte-for-byte identical to the current inline `NotFound(ex.Message)` / `BadRequest(ex.Message)` responses for the same exception instance and message.
3. THE refactor SHALL preserve, for every controller action, the request route, HTTP method, and response DTO type and structure without addition, removal, or renaming of any field.
4. WHEN the existing integration and unit test suite is executed after the refactor, THE test suite SHALL complete with zero failed tests and zero skipped tests attributable to the refactor.
5. THE refactor SHALL add tests that assert the Central_Exception_Handler returns the same HTTP status code and response-body message for `KeyNotFoundException` (not-found status), `InvalidOperationException` (validation-failure status), and `ArgumentException` (validation-failure status) as the previous inline try/catch produced for the same exception message.
6. IF the Central_Exception_Handler receives an exception type other than `KeyNotFoundException`, `InvalidOperationException`, or `ArgumentException`, THEN THE refactor SHALL preserve the pre-refactor server-error status (500) response with no exception detail exposed in the response body.
7. THE refactor SHALL add tests that assert the Api_Client_Mapping_Helper returns `ApiResult.Success` / `ApiResult<T>.Success` on a success response (including correct deserialization of the response body and preservation of null-default behavior when the body is empty) and returns `ApiResult.Failure` carrying the response body text on a non-success response.

### Requirement 5: Coding standards, layering, and non-goals

**User Story:** As a template maintainer, I want the new seams to follow the project's engineering conventions and architectural boundaries, so that the refactor is consistent with the rest of the template and does not introduce new projects or cross-layer dependencies.

#### Acceptance Criteria

1. THE new code SHALL use traditional constructors with explicit field assignments and SHALL NOT use primary constructors.
2. THE new public classes, interfaces, methods, and properties SHALL each include an XML documentation `<summary>`, and each new private field (instance and static) SHALL include a `<summary>`, per the coding-standards steering.
3. THE new code comments SHALL describe current behavior in present tense and SHALL NOT reference removed inline try/catch, prior implementations, migrations, or refactoring context.
4. THE Central_Exception_Handler SHALL reside in the ApiService project and in no other project.
5. THE Api_Client_Mapping_Helper, the Query_String_Helper, and the Client_Registration_Helper SHALL reside in the Web_Project and in no other project.
6. THE refactor SHALL NOT add the Central_Exception_Handler, the Api_Client_Mapping_Helper, the Query_String_Helper, or the Client_Registration_Helper to the Domain, Application, or Infrastructure projects.
7. THE refactor SHALL NOT create a new Shared project.
8. THE refactor SHALL NOT create a new Common project.
9. THE refactor SHALL NOT add, remove, or alter any DTO field, endpoint, route, or public member or type of the Web-side `ApiResult` / `ApiResult<T>` contract.
10. THE refactor SHALL NOT remove, replace, disable, or restructure any existing template capability, per the template-guardrails steering.
11. THE refactor SHALL NOT modify the "Consider later" audit items, namely the per-circuit context base class and the bulk-action UI flow.
12. IF a proposed change would violate any non-goal in this requirement, THEN the change SHALL be rejected and the corresponding existing artifact SHALL be preserved unchanged.
