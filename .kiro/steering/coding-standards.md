# Coding Standards & Instructions

## Documentation

### XML Documentation Comments
- ALL public classes, interfaces, methods, and properties MUST have `<summary>` XML docs.
- ALL private methods MUST have at least a `<summary>` tag explaining their purpose.
- Use `<param>`, `<returns>`, `<remarks>`, and `<exception>` tags where appropriate.
- State fields and properties in code-behind files MUST have `<summary>` explaining their role.
- Enum values MUST have `<summary>` explaining when each is used.

### Inline Comments
- EF Core configurations: explain rationale (why cascade delete, why specific index, etc.)
- Complex logic: annotate the algorithm or business rule being implemented.
- Non-obvious patterns: explain the "why" not just the "what".

## Code Organization

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

Rules:
- Every field, constructor, and method lives inside a region — nothing is left loose.
- The first region is always `#region Constructor`.
- Domain groups use descriptive names: "CRUD Operations", "Activation", "Query Operations", "Write Operations", etc.
- Private helpers are always the last region in service files.

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

## Conventions

### Naming
- Feature directories: kebab-case (e.g., `audit-log-old-new-values`)
- C# files: PascalCase matching the class name
- CSS classes: kebab-case

### MudBlazor
- Use `MudDrawerHeader` for sidebar headers (not custom divs)
- Use MudBlazor utility classes for spacing/flex (`d-flex`, `align-center`, `pa-4`, etc.)
- Prefer `Elevation="0"` on MudPaper for flat card style with border

### Testing
- Property-based tests: FsCheck.Xunit 3.x with `[Property(MaxTest = 2)]`
- Unit tests: xUnit + Moq
- Database tests: Microsoft.EntityFrameworkCore.Sqlite in-memory
- Test tag format: `// Feature: {feature-name}, Property {N}: {title}`

### Specs & Docs
- `.kiro/specs/{feature-name}/` — active spec documents (requirements.md, design.md, tasks.md)
- `docs/features/{feature-name}/` — archived/completed feature documentation

## Before Writing Code
- Read relevant existing code to match the project's style, conventions, and libraries.
- Check the project structure and existing patterns before introducing new approaches.
- Observe the existing folder structure and place new files accordingly.
