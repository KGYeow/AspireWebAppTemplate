# Coding Standards & Instructions

## Documentation

### XML Documentation Comments
- ALL public classes, interfaces, methods, and properties MUST have `<summary>` XML docs.
- ALL private methods MUST have at least a `<summary>` tag explaining their purpose.
- ALL private fields (instance and static) MUST have `<summary>` explaining their role — in services, handlers, contexts, and code-behind files alike.
- Use `<param>`, `<returns>`, `<remarks>`, and `<exception>` tags where appropriate.
- Enum values MUST have `<summary>` explaining when each is used.

### Comment Tone & Semantics
- Comments MUST describe the current functionality as-is — what the code does NOW.
- Comments MUST NOT reference historical changes, previous implementations, migrations, or refactoring context (e.g., "previously lived in X", "was removed from Y", "replaces the old Z").
- Use git history and spec documents for historical context — code comments are not changelogs.
- Write in present tense describing current behavior, not past tense describing what changed.
- Good: "Applies the filtering pipeline: auth → permissions → group visibility → decorations."
- Bad: "This service replaces the client-side filtering that previously lived in NavMenu."

### Inline Comments
- EF Core configurations: explain rationale (why cascade delete, why specific index, etc.)
- Complex logic: annotate the algorithm or business rule being implemented.
- Non-obvious patterns: explain the "why" not just the "what".

## Code Organization

### Constructor Style
- Use **traditional constructors** with explicit field assignments — NOT primary constructors.
- This applies to all classes: services, controllers, handlers, contexts, and typed HttpClients.
- Reason: primary constructors make class declarations too long when multiple dependencies are involved and obscure the field declarations.

```csharp
// GOOD: Traditional constructor
public class FooService : IFooService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<FooService> _logger;

    public FooService(ApplicationDbContext dbContext, ILogger<FooService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }
}

// BAD: Primary constructor
public class FooService(ApplicationDbContext dbContext, ILogger<FooService> logger) : IFooService
```

### Region Structure (Services & Controllers)
All service implementations and controllers use consistent `#region` grouping:

**Service pattern:**
```csharp
public class FooService : IFooService
{
    #region Constructor       // fields (instance + static) + constructor
    #region [Domain Group]    // public methods grouped by domain area
    #region Private Helpers   // private utility methods (mapping, validation)
}
```

**Controller pattern:**
```csharp
public class FooController : BaseController
{
    #region Constructor       // service field + constructor
    #region [Domain Group]    // endpoints grouped by domain area (CRUD, Activation, etc.)
}
```

**Interface pattern:**
```csharp
public interface IFooService
{
    #region [Domain Group]    // method signatures grouped by domain area
}
```

**Context pattern (per-circuit scoped services):**
```csharp
public class FooContext : IFooContext
{
    #region Constructor           // fields + constructor
    #region Properties and Events // public properties + events
    #region Initialization        // InitializeAsync and related startup logic
    #region [Domain Group]        // domain-specific methods (e.g., "Hub Connection", "Count Mutations")
    #region Disposal              // IAsyncDisposable/IDisposable implementation
}
```

**Delegating handler pattern:**
```csharp
public class FooDelegatingHandler : DelegatingHandler
{
    #region Constructor       // fields + constructor (no region needed if class is <30 lines)
    // SendAsync override — no region needed for single-method handlers
}
```

**Authentication handler pattern:**
```csharp
public class FooAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    #region Constructor       // fields + constructor (no region needed if class is <30 lines)
    // HandleAuthenticateAsync override — no region needed for single-method handlers
}
```

**Typed HttpClient pattern:**
```csharp
public class FooClient
{
    #region Constructor       // fields (including const paths) + constructor
    #region [Domain Group]    // public methods grouped by operation type
}
```

Rules:
- Every field, constructor, and method lives inside a region — nothing is left loose.
- The first region is always `#region Constructor`.
- Domain groups use descriptive names: "CRUD Operations", "Activation", "Query Operations", "Write Operations", "Hub Connection", "Notification Callback", etc.
- Private helpers are always the last region in service files.
- Exception for very small classes (<30 lines total): regions may be omitted for single-method handlers.

### Blazor Code-Behind
- Use partial class pattern (`.razor.cs`) for all page components — no `@code` blocks in `.razor`.
- Organize with `#region` blocks: Injected Services → State → Lifecycle → Event Handlers → Helpers.
- Use `[Inject]` attribute on properties (not constructor injection) for Blazor components.

## Coding Patterns

### Loading States
- Grid-dominant pages (MudDataGrid): rely on the grid's built-in `Loading` property — do NOT add `PageContent` wrapper.
- Form/detail pages with async init: wrap content in `<PageContent IsLoading="...">` from the UI shared library.
- Static pages: no loading state needed.

### Error Handling
- API service methods (Web project): swallow exceptions and return result objects (success/error pattern).
- Service layer (ApiService): throw typed exceptions for business rule violations; swallow only in audit logging.
- Controllers: map service exceptions to HTTP status codes via try/catch (see api-patterns.md).
- UI save operations: show error via Snackbar or inline alert, revert state on failure.

### Server-Side SignalR Hub Connections
In Blazor Server, when a scoped service (e.g., `NotificationContext`) creates a `HubConnection` back to its own host:
- The connection is server-side code connecting to the same ASP.NET Core process — there is NO browser involved
- `UserIdentityDelegatingHandler` does NOT apply — it's for `HttpClient` pipelines (Web→API calls)
- The user's auth cookie must be **manually captured** from `IHttpContextAccessor` during construction (SSR phase) and forwarded via `options.Headers.Add("Cookie", cookie)` in `WithUrl`
- After the circuit establishes, `HttpContext` becomes null — capture must happen in the constructor or early lifecycle

```csharp
// Capture cookie during SSR (constructor or early init)
_authCookie = httpContextAccessor.HttpContext?.Request.Headers.Cookie.ToString();

// Forward when building the hub connection
_hubConnection = new HubConnectionBuilder()
    .WithUrl(hubUrl, options =>
    {
        if (!string.IsNullOrEmpty(_authCookie))
            options.Headers.Add("Cookie", _authCookie);
    })
    .WithAutomaticReconnect(new ExponentialBackoffRetryPolicy())
    .Build();
```

## Conventions

### Naming
- Feature directories: kebab-case (e.g., `audit-log-old-new-values`)
- C# files: PascalCase matching the class name
- CSS classes: kebab-case

### DateTime Conventions
- ALL DateTime properties use UTC and include a `Utc` suffix (e.g., `CreatedAtUtc`, `UpdatedAtUtc`).
- Use `DateTime` (NOT `DateTimeOffset`) — matches the existing entity and DTO convention.
- Store as UTC in the database, display in the user's timezone via `IUserTimeZoneContext.FormatDateTime()`.
- Never use `DateTime.Now` — always `DateTime.UtcNow`.
- Nullable `DateTime?` for optional timestamps (e.g., `UpdatedAtUtc` which is null until first edit).

### MudBlazor
- Use `MudDrawerHeader` for sidebar headers (not custom divs)
- Use MudBlazor utility classes for spacing/flex (`d-flex`, `align-center`, `pa-4`, etc.)
- Prefer `Elevation="0"` on MudPaper for flat card style with border

### Service Registration Extensions
- Web project: `Extensions/ApiClientServiceExtensions.cs` exposes `AddApiClients(this IServiceCollection)` — registers all typed HttpClient services. Uses a `private const string ApiServiceBaseAddress` for the Aspire service discovery URL (`"https+http://apiservice"`).
- Web project: `Extensions/ApplicationServiceExtensions.cs` exposes `AddApplicationServices(this IServiceCollection)` — registers scoped services, handlers, contexts.
- API project: `Extensions/ApplicationServiceExtensions.cs` exposes `AddApplicationServices(this IServiceCollection)` — registers all business services.
- `Program.cs` calls these extension methods instead of inline registrations.
- Group related registrations (e.g., all API clients together, all scoped services together) within the extension method.

### Asset Defaults
- Centralized logo and background paths in `Web/Common/Defaults/AssetDefaults.cs`.
- Reference via `@AssetDefaults.LogoAuth`, `@AssetDefaults.LogoSidebar`, `@AssetDefaults.BackgroundAuth`, etc.
- Never hardcode asset paths directly in Razor files — always go through `AssetDefaults`.

### Theme Configuration
- `DefaultTheme` (neutral blue) and `JabilTheme` (corporate brand) in `UI/Theme/`.
- Layouts declare: `protected JabilTheme AppTheme { get; } = new();`
- Swap `JabilTheme` → `DefaultTheme` for personal/unbranded deployments.
- Do NOT use `ApplicationTheme` — it no longer exists.

### Testing
- Property-based tests: FsCheck.Xunit 3.x with `[Property(MaxTest = 2)]`
- Unit tests: xUnit + Moq
- Database tests: Microsoft.EntityFrameworkCore.Sqlite in-memory
- Test tag format: `// Feature: {feature-name}, Property {N}: {title}`

### Seed Data
- Seed data lives in `Data/SeedData/` as partial class files (`SeedData.{Feature}.cs`).
- Each feature's seed method is wrapped in `#region {Feature}`.
- The main `SeedData.cs` contains `InitializeAsync` (orchestrates all seed methods) and shared helpers.
- Use upsert pattern: seed only if record doesn't already exist (preserve admin customizations on redeployment).
- System/reference data uses deterministic checks (e.g., `EmailType` enum value). Sample data uses existence checks (e.g., `if (await dbContext.Announcements.AnyAsync()) return;`).

### Server-Side Sorting & Pagination (Large Datasets)
- For pages with large datasets (audit log, future reporting), use true server-side sorting via `QueryableExtensions.ApplySort<T>` in `Core/Extensions/`.
- Query param DTOs (e.g., `AuditLogQueryParams`) include `SortBy` (string?) and `SortDescending` (bool) properties.
- Frontend extracts sort from `state.SortDefinitions.FirstOrDefault()` and sets the DTO properties.
- Typed HttpClient services accept the query param DTO as a single object (not flat parameters) for complex queries.
- `ApplySort` uses Expression trees for type-safe, EF Core-translatable dynamic ordering with a fallback default sort.

### Specs & Docs
- `.kiro/specs/{feature-name}/` — active spec documents (requirements.md, design.md, tasks.md)
- `docs/features/{feature-name}/` — archived/completed feature documentation

## Before Writing Code
- Read relevant existing code to match the project's style, conventions, and libraries.
- Check the project structure and existing patterns before introducing new approaches.
- Observe the existing folder structure and place new files accordingly.
