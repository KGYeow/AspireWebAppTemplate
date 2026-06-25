# UI Patterns & MudBlazor Conventions

## Layout Architecture

The layout uses MudBlazor's `MudLayout` system with region-based folder organization:

```
MudLayout
├── Topbar (MudAppBar) — app title, hamburger menu toggle, profile dropdown
├── MudDrawer (Mini/Responsive variant) — sidebar with DrawerHeader + NavMenu
├── MudMainContent (position: relative; min-vh-100) — page content area
│   └── MudContainer → @Body
└── Footer (MudAppBar Bottom)
```

### Sidebar
- `DrawerHeader` — shows full logo when open, mini logo when collapsed. Uses `MudDrawerHeader`.
- `NavMenu` — permission-filtered navigation using `PagePermissionContext.CanAccess()`.
- Simple `@if/@else` for logo swap between open/collapsed states (no CSS transitions).

## Loading States

### Page-Level Loading (PageContent wrapper)
Use for form/detail pages that fetch data in `OnInitializedAsync`:
```razor
<PageContent IsLoading="_isLoading">
    <!-- page content -->
</PageContent>
```
- Shows `LoadingOverlay` (centered spinner) covering the `MudMainContent` area.
- `MudMainContent` has `position: relative` so the overlay fills it correctly.
- Optional: provide `<LoadingContent>` for custom skeletons.

### Grid-Level Loading
For pages dominated by `MudDataGrid`, do NOT use `PageContent`. Use the grid's built-in:
```razor
<MudDataGrid Loading="@_isLoading" ...>
    <LoadingContent>Loading...</LoadingContent>
</MudDataGrid>
```

### In-Page Operations
For subsequent operations (save, refresh) where content already exists on screen:
```razor
<LoadingOverlay Visible="@_isSaving" Text="Saving..." />
```
This overlays semi-transparently on top of existing content.

## Component Patterns

### PageHeader
Standard page title component from UI shared library:
```razor
<PageHeader Title="Settings" Subtitle="Optional description" />
```

### MudPaper Cards
Use flat style (Elevation 0) for content sections:
```razor
<MudPaper Class="pa-4" Elevation="0">
    <!-- content -->
</MudPaper>
```

### Data Grids
- Use `MudDataGrid<T>` with `ServerData` callback for large datasets (audit log).
- Use `MudDataGrid<T>` with `Items` for small in-memory datasets (role management).
- Always include `<NoRecordsContent>` and `<LoadingContent>`.
- Use `QueryableDataGridUtils<T>` for database-level filtering/sorting/pagination.
- Use `DataGridUtils<T>` for in-memory filtering/sorting/pagination.

### Dialogs
Use `ConfirmationDialog` from UI shared library for destructive actions:
```csharp
var confirmed = await DialogService.ShowAsync<ConfirmationDialog>("Delete User", ...);
```

### Alerts & Notifications
- Inline alerts: `<MudAlert>` with `ShowCloseIcon` for dismissible messages.
- Snackbar: `Snackbar.Add(...)` for transient notifications (auto-dismiss).
- Error on save: show inline alert OR revert state + snackbar.

## Navigation

### DefaultNavigationProvider
- Defines the full navigation structure (groups, links, icons, hrefs).
- `AuthorizedOnly = true` gates items from anonymous users.
- Role-based visibility is handled by `PagePermissionContext`, NOT by `NavItem.Roles`.

### NavMenu Filtering Pipeline
1. Loading check → skeleton placeholder
2. Auth-based: `AuthorizedOnly` / `NotAuthorizedOnly`
3. Permission-based: `PagePermissionContext.CanAccess(href)`
4. Group visibility: hide groups with zero visible children
5. System_Pages always visible regardless of permissions

## Theme
- Three modes: Light, Dark, System (follows OS preference).
- Theme preference stored per-user in database.
- `IThemeContext` scoped service notifies layout of changes in real time.
- Theme toggle via `PillToggle` component on Settings page.
