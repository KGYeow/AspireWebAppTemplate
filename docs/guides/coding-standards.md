# Coding Standards

## File Organization

### Feature-per-folder
Each feature lives in its own folder under `Components/Pages/`:
```
Components/Pages/{Feature}/
├── Index.razor
├── Index.razor.cs
├── Details.razor (optional)
├── Details.razor.cs (optional)
└── {Feature}Dialog.razor (optional)
```

### Code-behind pattern
Always use separate `.razor.cs` files — no `@code` blocks in Razor files.

## Naming Conventions

| Item | Convention | Example |
|------|-----------|---------|
| Pages | `Index.razor` in feature folder | `Pages/Settings/Index.razor` |
| Dialogs | `{Action}{Entity}Dialog.razor` | `AddUserDialog.razor` |
| Services | `I{Name}Service` + `{Name}Service` | `IAuditLogService`, `AuditLogService` |
| ViewModels | `{Entity}ViewModel` | `UserViewModel`, `RoleViewModel` |
| Enums | PascalCase values | `ThemePreference.Dark` |
| Form models | Nested `InputModel` class | `private sealed class InputModel` |

## MudBlazor Patterns

### Labels
```razor
<!-- Always use separate MudInputLabel, never the built-in Label prop -->
<MudInputLabel Class="fw-bold">Field Name</MudInputLabel>
<MudTextField @bind-Value="value" Variant="Variant.Outlined" Margin="Margin.Dense" Typo="Typo.body2" />
```

### Section containers
```razor
<MudPaper Class="pa-4 mb-4" Elevation="0">
    <MudText Typo="Typo.h6" Class="mb-3">Section Title</MudText>
    <!-- content -->
</MudPaper>
```

### View Mode values
```razor
<MudInputLabel Class="fw-bold">Label</MudInputLabel>
<MudText Typo="Typo.body2">@(value ?? "-")</MudText>
```

## Service Registration

| Lifetime | Use for |
|----------|---------|
| Singleton | Stateless services (TimeZoneService) |
| Scoped | Per-circuit state (ThemeStateService, UserTimeZoneContext, AuditLogService) |
| Transient | Avoid unless specifically needed |

## Error Handling

- Services that support primary operations (audit logging) swallow exceptions and log at Error level
- Services that ARE the primary operation propagate exceptions to callers
- UI shows dismissible MudAlert for user-facing errors
- Log structured data (userId, actionType) not raw exception messages to users

## Validation

- Use `OptionalPhoneAttribute` (not `[Phone]`) for phone number fields
- Use `[MaxLength]` on all string properties
- Use `DataAnnotationsValidator` in EditForm
- Form model is always a nested `private sealed class InputModel`
