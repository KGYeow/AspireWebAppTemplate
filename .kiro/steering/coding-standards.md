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

## Coding Patterns

### Blazor Code-Behind
- Use partial class pattern (`.razor.cs`) for all page components — no `@code` blocks in `.razor`.
- Organize with `#region` blocks: Injected Services → State → Lifecycle → Event Handlers → Helpers.
- Use `[Inject]` attribute on properties (not constructor injection) for Blazor components.

### Loading States
- Grid-dominant pages (MudDataGrid): rely on the grid's built-in `Loading` property — do NOT add `PageContent` wrapper.
- Form/detail pages with async init: wrap content in `<PageContent IsLoading="...">` from the UI shared library.
- Static pages: no loading state needed.

### Error Handling
- API service methods: swallow exceptions and return result objects (success/error pattern).
- Audit logging: swallow database exceptions, log at Error level, never disrupt the primary operation.
- UI save operations: show error via Snackbar or inline alert, revert state on failure.

## Project Structure

### Layout Folder (Web project)
```
Components/Layout/
├── MainLayout.razor (+.cs)     ← Entry-point layouts at root
├── AuthLayout.razor (+.cs)
├── ManageLayout.razor
├── Topbar/                     ← Topbar region components
├── Sidebar/                    ← Drawer header, nav menu, etc.
├── Footer/                     ← Footer component
└── Shared/                     ← Cross-layout utilities (ReconnectModal, etc.)
```

### Shared Components
- `AspireWebAppTemplate.UI/Components/Shared/` — reusable, project-agnostic components (PageContent, LoadingOverlay, PageHeader, ConfirmationDialog, etc.)
- `AspireWebAppTemplate.Web/Components/Shared/` — web-project-specific shared components

### Specs & Docs
- `.kiro/specs/{feature-name}/` — active spec documents (requirements.md, design.md, tasks.md)
- `docs/features/{feature-name}/` — archived/completed feature documentation

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

## Before Writing Code
- Read relevant existing code to match the project's style, conventions, and libraries.
- Check the project structure and existing patterns before introducing new approaches.
- Observe the existing folder structure and place new files accordingly.
