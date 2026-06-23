# API & Service Layer Patterns

## Thin Controller / Full Service Layer (Industry Standard)

The project follows a strict **thin controller + full service layer** architecture. Controllers handle ONLY HTTP concerns; all business logic lives in service implementations.

### Controller Responsibilities (ONLY these)
1. Receive HTTP request and extract route/query/body parameters
2. Read `CurrentUserId` / `CurrentUserName` / `ClientIpAddress` from `BaseController`
3. Perform input-format validation (e.g., max ID count checks)
4. Call the appropriate service method, passing userId and request data
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
- All database access via `ApplicationDbContext`
- All entity-to-DTO mapping/projection
- Audit logging (snapshot, mutate, compute changes, log)
- Cross-cutting concerns (e.g., notification creation after user events)
- Interaction with `UserManager`, `RoleManager`, `SignInManager`

### Reference Implementation
`NotificationController` → `INotificationService` / `NotificationService` is the reference pattern. Every controller should look like this.

```csharp
// GOOD: Thin controller — delegates everything to service
[HttpPut("{id:guid}/read")]
public async Task<IActionResult> MarkAsRead(Guid id)
{
    var found = await _notificationService.MarkAsReadAsync(CurrentUserId!, id);
    if (!found) return NotFound();
    return Ok();
}

// BAD: Fat controller — business logic inline
[HttpPut("{id:guid}/read")]
public async Task<IActionResult> MarkAsRead(Guid id)
{
    var notification = await _dbContext.Notifications
        .FirstOrDefaultAsync(n => n.Id == id && n.UserId == CurrentUserId);
    if (notification is null) return NotFound();
    if (notification.IsRead) return Ok(); // business logic leak
    notification.IsRead = true;
    notification.ReadAtUtc = DateTime.UtcNow;
    await _dbContext.SaveChangesAsync();
    return Ok();
}
```

## Controller Conventions

### Base Pattern
All controllers extend `BaseController` which provides:
- `CurrentUserId` — authenticated user's ID from claims
- `CurrentUserName` — authenticated user's name from claims
- `ClientIpAddress` — source IP from `HttpContext.Connection`

### Route Conventions
```csharp
[Route("api/[controller]")]     // Standard entity controllers
[Route("api/page-permissions")] // Kebab-case for multi-word routes
```

### Authorization
- Admin-only endpoints: `[Authorize(Roles = "Admin")]`
- Authenticated users: `[Authorize]` (global via _Imports.razor for pages)
- Public endpoints: `[AllowAnonymous]`

### Response Patterns
- Success: `Ok()`, `Ok(data)`, `CreatedAtAction(...)`.
- Validation error: `BadRequest(message)` with human-readable error text.
- Not found: `NotFound()` or `NotFound(message)`.
- Server error: let middleware handle (500).

### Exception-to-Status Mapping
Every controller action that delegates to a service should use this pattern:
```csharp
try
{
    var result = await _service.DoSomethingAsync(CurrentUserId!, request);
    return Ok(result);
}
catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
catch (ArgumentException ex)         { return BadRequest(ex.Message); }
```

## Service Layer

### Interface Location
- Service interfaces: `ApiService/Abstractions/` (e.g., `IAuditLogService`, `IPagePermissionService`, `INotificationService`)
- Shared interfaces: `Core/Application/Abstractions/` (e.g., `INavigationProvider`)

### Implementation Location
- `ApiService/Services/` — business logic implementations

### DI Registration
- Services registered as **scoped** (aligns with per-request DbContext lifetime).
- Register in `Program.cs`.

### Service Method Signatures
- For **user-scoped queries** (e.g., "get MY notifications"): accept `userId` as a parameter since the service needs to know whose data to fetch. The controller passes `CurrentUserId`.
- For **admin operations** (e.g., "update user X"): accept the target entity ID as a parameter. The acting user's identity comes from `ICurrentUserAccessor` (for audit logging), not a method parameter.
- Accept request DTOs for mutations, query param DTOs for queries
- Return DTOs or `PagedResult<TDto>` for queries
- Return `bool` for found/not-found operations (e.g., MarkAsRead)
- Return `int` for count-of-affected operations (e.g., BulkDismiss, MarkAllAsRead)
- Throw `KeyNotFoundException` when entity not found
- Throw `InvalidOperationException` / `ArgumentException` for business rule violations

## Audit Logging

### Where Audit Logging Lives
Audit logging is a **service-layer responsibility**, NOT a controller responsibility. The service performs the full audit cycle:

1. Snapshot entity state before mutation
2. Apply the mutation
3. Snapshot entity state after mutation
4. Compute the diff
5. Call `IAuditLogService.LogAsync(...)` with the diff

### Current User Context via ICurrentUserAccessor
Services that need the authenticated user's identity (for audit logging, ownership checks, etc.) inject `ICurrentUserAccessor` — a scoped service backed by `IHttpContextAccessor`:

```csharp
// Interface in ApiService/Abstractions/
public interface ICurrentUserAccessor
{
    string? UserId { get; }
    string? UserName { get; }
    string? IpAddress { get; }
}
```

This eliminates passing `userId` and `ipAddress` through every method signature. Services read the current user from the accessor directly:

```csharp
// Inside service implementation
public class UserService : IUserService
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IAuditLogService _auditLogService;

    public async Task UpdateUserAsync(string targetUserId, UpdateUserRequest request)
    {
        // ... business logic ...

        await _auditLogService.LogAsync(new AuditLogRequest
        {
            UserId = _currentUser.UserId,       // from accessor, not parameter
            IpAddress = _currentUser.IpAddress,  // from accessor, not parameter
            ActionType = AuditActionType.UserUpdated,
            // ...
        });
    }
}
```

**Unit testing:** Mock `ICurrentUserAccessor` to return a fixed user identity in tests.

### AuditChangeHelper Methods
- `Snapshot<T>(entity, fields)` — creates dictionary of field values
- `ComputeChanges(before, after)` — returns `(oldJson, newJson)` with only changed fields
- `Serialize(object)` — camelCase JSON serialization (null-preserving)

### Audit Field Arrays
Define once per entity as a static field in the **service** (not controller):
```csharp
private static readonly (string, Func<ApplicationUser, object?>)[] UserAuditFields = [...];
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
- Audit logging failures: swallow + log at Error level (never disrupt primary operation)
- Database exceptions in business operations: let propagate to controller for standard error handling

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

### Auth Propagation
`UserIdentityDelegatingHandler` forwards the authenticated user's identity headers to the API service on every request.

### Service Discovery
Registered with Aspire: `"https+http://apiservice"` base address.
