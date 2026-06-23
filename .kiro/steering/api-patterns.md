# API & Service Layer Patterns

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

## Service Layer

### Interface Location
- Service interfaces: `ApiService/Abstractions/` (e.g., `IAuditLogService`, `IPagePermissionService`)
- Shared interfaces: `Core/Application/Abstractions/` (e.g., `INavigationProvider`)

### Implementation Location
- `ApiService/Services/` — business logic implementations

### DI Registration
- Services registered as **scoped** (aligns with per-request DbContext lifetime).
- Register in `Program.cs` service extension methods.

## Audit Logging

### How to Add Audit Logging to a Controller Action
```csharp
// 1. For update operations — snapshot before/after and compute diff
var before = AuditChangeHelper.Snapshot(entity, EntityAuditFields);
// ... apply mutations ...
var after = AuditChangeHelper.Snapshot(entity, EntityAuditFields);
var (oldValues, newValues) = AuditChangeHelper.ComputeChanges(before, after);

// 2. Construct and log
await _auditLogService.LogAsync(new AuditLogRequest
{
    UserId = CurrentUserId,
    ActionType = AuditActionType.UserUpdated,
    EntityType = AuditEntityType.User,
    EntityId = entity.Id,
    EntityName = entity.DisplayName ?? "",
    Description = $"User '{entity.DisplayName}' was updated.",
    OldValues = oldValues,
    NewValues = newValues,
    IpAddress = ClientIpAddress
});
```

### AuditChangeHelper Methods
- `Snapshot<T>(entity, fields)` — creates dictionary of field values
- `ComputeChanges(before, after)` — returns `(oldJson, newJson)` with only changed fields
- `Serialize(object)` — camelCase JSON serialization (null-preserving)

### Field Arrays
Define once per entity as a static field in the controller:
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

### Controller Level
```csharp
try
{
    // Business logic
    return Ok(result);
}
catch (KeyNotFoundException ex)   { return NotFound(ex.Message); }
catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
catch (ArgumentException ex)     { return BadRequest(ex.Message); }
```

### Service Level
- Database exceptions in `LogAsync`: swallow + log at Error level.
- Database exceptions in business operations: propagate to controller.
- Validation: throw `ArgumentException` or `InvalidOperationException` with clear messages.

## Web → API Communication

### HttpClient Services (Web project)
Located in `Web/Services/ApiClients/`:
```csharp
public class ApiUserService
{
    private readonly HttpClient _httpClient; // configured with Aspire service discovery
    // Methods return ApiResult<T> — never throw on HTTP errors
}
```

### Auth Propagation
`UserIdentityDelegatingHandler` forwards the authenticated user's identity headers to the API service on every request.

### Service Discovery
Registered with Aspire: `"https+http://apiservice"` base address.
