# API & Service Layer Patterns

## Thin Controller / Full Service Layer

The project follows a strict **thin controller + full service layer** architecture. Controllers handle ONLY HTTP concerns; all business logic lives in service implementations.

### Controller Responsibilities (ONLY these)
1. Receive HTTP request and extract route/query/body parameters
2. Read `CurrentUserId` / `CurrentUserName` / `ClientIpAddress` from `BaseController`
3. Perform input-format validation (e.g., max ID count checks)
4. Call the appropriate service method
5. Map the service result (or exception) to an HTTP status code
6. Return the response

### Controller MUST NOT
- Inject or use `ApplicationDbContext` directly
- Contain EF Core queries (Where, Select, Include, etc.)
- Perform business validation (e.g., "is this a system role?", "does user exist?")
- Orchestrate multi-step operations (create + assign role + audit)
- Construct audit log entries
- Contain conditional business logic (if/else based on entity state)
- Define entity-to-DTO mapping logic

### Service Responsibilities
- All business logic: validation, guards, conditional flows
- All database access via `ApplicationDbContext` or Identity managers
- All entity-to-DTO mapping/projection
- Audit logging for security-sensitive operations
- Cross-cutting concerns (e.g., notification creation after user events)
- Interaction with `UserManager`, `RoleManager`, `SignInManager`

### Reference Implementation
`NotificationController` → `INotificationService` / `NotificationService` is the reference pattern.

```csharp
// GOOD: Thin controller — delegates everything to service
[HttpPut("{id:guid}/read")]
public async Task<IActionResult> MarkAsRead(Guid id)
{
    var found = await _notificationService.MarkAsReadAsync(CurrentUserId!, id);
    if (!found) return NotFound();
    return Ok();
}
```

## Controller Conventions

### Base Pattern
All controllers extend `BaseController` which provides:
- `CurrentUserId` — authenticated user's ID from claims
- `CurrentUserName` — authenticated user's name from claims
- `ClientIpAddress` — client IP from `X-Client-Ip` header (forwarded by Web project)

### Route Conventions
```csharp
[Route("api/[controller]")]     // Standard entity controllers
[Route("api/page-permissions")] // Kebab-case for multi-word routes
```

### Authorization
- Admin/restricted endpoints: `[Authorize]` (page permission system controls access via database-driven whitelist; will evolve to permission-based policies)
- Authenticated users: `[Authorize]`
- Public endpoints: `[AllowAnonymous]`

### Response Patterns
- Success: `Ok()`, `Ok(data)`, `CreatedAtAction(...)`.
- Validation error: `BadRequest(message)` with human-readable error text.
- Not found: `NotFound()` or `NotFound(message)`.
- Server error: let middleware handle (500).

### Exception-to-Status Mapping
Every controller action that delegates to a service uses this pattern:
```csharp
try
{
    var result = await _service.DoSomethingAsync(request);
    return Ok(result);
}
catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
catch (ArgumentException ex)         { return BadRequest(ex.Message); }
```

## Service Layer

### Interface Location
- Service interfaces: `ApiService/Abstractions/` (e.g., `IAuditLogService`, `IRoleService`, `IUserService`, `IAuthService`)
- Shared interfaces: `Core/Application/Abstractions/` (e.g., `INavigationProvider`)

### Implementation Location
- `ApiService/Services/` — business logic implementations

### DI Registration
- Services registered as **scoped** (aligns with per-request DbContext lifetime).
- API project: registered via `Extensions/ApplicationServiceExtensions.cs` → `AddApplicationServices()`.
- Web project: HTTP clients via `Extensions/ApiClientServiceExtensions.cs` → `AddApiClients()`; other services via `Extensions/ApplicationServiceExtensions.cs` → `AddApplicationServices()`.
- `Program.cs` calls these extension methods — no inline service registrations.

### Service Method Signatures
- For **user-scoped queries** (e.g., "get MY notifications"): accept `userId` as a parameter. The controller passes `CurrentUserId`.
- For **self-management operations** (e.g., "update MY profile"): no userId parameter needed. The service resolves the current user via `ICurrentUserAccessor`.
- For **admin operations** (e.g., "update user X"): accept the target entity ID as a parameter. The acting user's identity comes from `ICurrentUserAccessor` for audit logging.
- Accept request DTOs for mutations, query param DTOs for queries.
- Return DTOs or `PagedResult<TDto>` for queries.
- Return `bool` for found/not-found operations.
- Return `int` for count-of-affected operations.
- Throw `KeyNotFoundException` when entity not found.
- Throw `InvalidOperationException` / `ArgumentException` for business rule violations.
- Always check Identity operation results: `if (!result.Succeeded)` → throw with concatenated error descriptions.

### ICurrentUserAccessor
A scoped service that provides the authenticated user's identity to service-layer components:

```csharp
public interface ICurrentUserAccessor
{
    string? UserId { get; }
    string? UserName { get; }
    string? IpAddress { get; }
}
```

- Reads `UserId` from `ClaimTypes.NameIdentifier`
- Reads `UserName` from `Identity.Name`
- Reads `IpAddress` from `X-Client-Ip` header (forwarded by Web project), falling back to `Connection.RemoteIpAddress`
- Returns null for all properties when no HTTP context or authenticated user is available

## Audit Logging

### Scope
Audit logging covers **security-sensitive operations only**:
- Admin actions: user/role CRUD, activation/deactivation, role assignment
- Authentication events: login success/failure, logout
- Account security: password change, email change, 2FA enable/disable/reset, account deletion

Personal actions (profile edits, preference changes) are **not audited** — these are privacy-respecting user choices.

### Where Audit Logging Lives
Audit logging is a **service-layer responsibility**. The service performs:
1. Snapshot entity state before mutation
2. Apply the mutation
3. Snapshot entity state after mutation
4. Compute the diff
5. Call `IAuditLogService.LogAsync(...)` with the diff

### AuditChangeHelper Methods
- `Snapshot<T>(entity, fields)` — creates dictionary of field values
- `ComputeChanges(before, after)` — returns `(oldJson, newJson)` with only changed fields
- `Serialize(object)` — camelCase JSON serialization (null-preserving)

### Audit Field Arrays
Define once per entity as a static field in the service's `#region Constructor`:
```csharp
private static readonly (string Key, Func<ApplicationUser, object?> Getter)[] UserAuditFields = [...];
```

### Rules
- Audit failures NEVER disrupt primary operations (swallowed, logged at Error level).
- Only changed fields in OldValues/NewValues (not full entity snapshots).
- Sensitive fields (PasswordHash, SecurityStamp) NEVER appear in audit values.
- CamelCase JSON output via `JsonNamingPolicy.CamelCase`.
- Null values serialized as JSON `null` (not omitted).

## DTO Conventions

### Location
- `Core/Contracts/{Feature}/` — grouped by feature (e.g., `Contracts/AuditLog/`, `Contracts/Users/`)

### Naming
- Request DTOs: `{Action}Request` (e.g., `UpdateProfileRequest`, `CreateUserRequest`)
- Response DTOs: `{Entity}Dto` (e.g., `UserDto`, `RoleDto`)
- Result wrappers: `ApiResult<T>` with `Succeeded`, `Data`, `Error` properties

## Error Handling

### Service Level
- Business rule violations: throw `InvalidOperationException` or `ArgumentException` with clear messages
- Entity not found: throw `KeyNotFoundException`
- Identity operation failures: throw `InvalidOperationException` with concatenated error descriptions
- Audit logging failures: swallow + log at Error level (never disrupt primary operation)

### Controller Level
- Map service exceptions to HTTP status codes (see Exception-to-Status Mapping above)
- Input-format validation (e.g., >100 IDs → 400) happens BEFORE calling the service

## Web → API Communication

### HttpClient Services (Web project)
Located in `Web/Services/ApiClients/`:
```csharp
public class ApiUserService(HttpClient http)
{
    // Methods return ApiResult<T> — never throw on HTTP errors
}
```

### Identity & IP Propagation
`UserIdentityDelegatingHandler` forwards:
- Authenticated user's claims (identity headers)
- Client IP address via `X-Client-Ip` header (read from the Web project's `HttpContext.Connection.RemoteIpAddress`)

### Service Discovery
Registered with Aspire: `"https+http://apiservice"` base address.

## API → Web Communication (Internal Callbacks)

### Pattern
The API project calls back to the Web project for real-time notification delivery. This is a reverse direction from the normal Web→API flow.

### Components
- **`WebCallbackClient`** (`ApiService/Services/WebCallbackClient.cs`) — typed HttpClient that POSTs to the Web project's internal endpoint
- **`InternalApiKeyDelegatingHandler`** (`ApiService/Services/Handlers/`) — attaches `X-Internal-Api-Key` header to outbound requests
- **`InternalApiKeyAuthenticationHandler`** (`Web/Authentication/`) — validates the API key on the Web side
- **`NotificationCallbackEndpoint`** (`Web/Endpoints/`) — minimal API endpoint that receives the callback and pushes to SignalR

### Service Discovery
Registered with Aspire: `"https+http://webfrontend"` base address (reverse direction).

### Authentication
- Shared secret via `INTERNAL_API_KEY` environment variable (set by Aspire AppHost parameter)
- API attaches: `InternalApiKeyDelegatingHandler` adds `X-Internal-Api-Key` header
- Web validates: `InternalApiKeyAuthenticationHandler` + `"InternalApiPolicy"` authorization policy

### Error Handling
- Callback failures (timeout, network, non-success status) are logged at Warning level and **never** disrupt the primary operation
- The notification is already persisted in the database — real-time delivery is best-effort
- No retry logic — the user sees the notification on next page load if real-time fails
