# TODO: Refactor User/Role ViewModels to Wrapper Pattern

## Summary

Refactor `UserViewModel` and `RoleViewModel` in the User Management and Role Management admin pages from the flat ViewModel pattern (duplicates all DTO properties) to the wrapper pattern (holds a DTO reference + delegates properties). This aligns them with the Announcement and Email Template pages.

## Current State (Flat Pattern)

```csharp
private sealed class RoleViewModel
{
    public int LineNumber { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; }
    public int UserCount { get; set; }
    public int Position { get; set; }
    // ... all properties duplicated from RoleDto
}

// Mapping (verbose):
new RoleViewModel { Id = dto.Id, Name = dto.Name, DisplayName = dto.DisplayName, ... }
```

## Target State (Wrapper Pattern)

```csharp
private sealed class RoleViewModel
{
    public int LineNumber { get; set; }
    public RoleDto Role { get; set; } = default!;

    // Delegated from DTO
    public string Id => Role.Id;
    public string Name => Role.Name;
    public string DisplayName => Role.DisplayName;
    public bool IsActive => Role.IsActive;
    public int Position => Role.Position;

    // Computed (not on DTO)
    public int UserCount { get; set; }

    // Required for multi-selection HashSet tracking
    public override bool Equals(object? obj) => obj is RoleViewModel other && Id == other.Id;
    public override int GetHashCode() => Id.GetHashCode();
}

// Mapping (clean):
new RoleViewModel { Role = dto, UserCount = computedCount }
```

## Files to Update

### Role Management
- `Web/Components/Pages/Admin/RoleManagement/Index.razor.cs`
  - Rewrite `RoleViewModel` to wrapper pattern
  - Update `LoadRoleViewModelsAsync` mapping
  - Update event handlers to pass `vm.Role` to dialogs
- `Web/Components/Pages/Admin/RoleManagement/Index.razor`
  - No changes needed (property names stay the same)

### User Management
- `Web/Components/Pages/Admin/UserManagement/Index.razor.cs`
  - Rewrite `UserViewModel` to wrapper pattern
  - Update `LoadUserViewModelsAsync` mapping
  - Update event handlers to pass `vm.User` to dialogs
- `Web/Components/Pages/Admin/UserManagement/Index.razor`
  - No changes needed (property names stay the same)

## Benefits
- Consistent pattern across all 4 admin DataGrid pages
- Less mapping boilerplate
- `vm.Role` / `vm.User` gives instant access to DTO for dialog parameters and API calls
- No re-fetch needed when passing data to dialogs
- Automatically reflects new DTO properties without ViewModel changes

## Notes
- Keep computed properties (e.g., `UserCount`) as settable properties on the ViewModel
- Add `Equals`/`GetHashCode` based on Id for pages with multi-selection
- Delegated properties use `=>` (expression-bodied, read-only)
- This is a pure refactoring — no behavior changes
