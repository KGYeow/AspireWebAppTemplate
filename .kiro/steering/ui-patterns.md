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

### StatusAlert Component
Self-hiding success/error alert from UI shared library. Replaces manual `<MudAlert>` boilerplate:
```razor
<StatusAlert @bind-Message="_successMessage" Severity="Severity.Success" />
<StatusAlert @bind-Message="_errorMessage" Severity="Severity.Error" />
```
- Auto-hides after a timeout (configurable).
- Supports `@bind-Message` — set message to show, clears automatically on dismiss/timeout.
- Dismissible via close icon.
- Dense mode for compact layouts.
- Use instead of duplicating MudAlert show/hide logic per page.

### Notification Bell Dropdown
The topbar notification bell uses `MudMenu` (not MudPopover):
```razor
<MudMenu Icon="@Icons.Material.Filled.Notifications" ...>
    <!-- MudMenuItem for each notification -->
</MudMenu>
```
- Skeleton loading while notifications fetch.
- Category icons wrapped in circle containers for visual consistency.
- Badge count on the bell icon for unread notifications.
- Real-time events delivered via `NotificationReceivedEventArgs` (strongly-typed event args with Title, Message, Category, NotificationId).
- Snackbar toast click navigates to `/account/notifications?id={notificationId}` for deep-link expansion.

### NotificationSnackbarContent
Custom snackbar content component in UI shared library (`NotificationSnackbarContent.razor`):
- Uses `MudStack Row` with `AlignItems.Start` (top-aligned icon to support multi-line messages).
- Icon avatar with category-specific color class.
- Title (bold, body2) and message (caption), both with text-overflow ellipsis.
- Cursor pointer to indicate clickability (deep-link navigation handled by snackbar's `Onclick`).

### Notification Page (Master-Detail Layout)
The full notifications page uses a master-detail pattern:
- Left panel: scrollable notification list with infinite scroll (load more on scroll end).
- Right panel: selected notification detail view.
- Action menu per notification item (mark read, delete, etc.).

### Notification Settings
Uses `MudSimpleTable` with checkbox columns for per-category preferences:
```razor
<MudSimpleTable>
    <!-- Rows per notification category, columns for each channel (Email, InApp, etc.) -->
</MudSimpleTable>
```

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
- `DefaultTheme` — neutral blue palette for personal/non-branded use.
- `JabilTheme` — Jabil corporate brand palette.
- Layouts declare: `protected JabilTheme AppTheme { get; } = new();` (swap to `DefaultTheme` for unbranded deployments).

## Asset Defaults
Centralized asset paths via `AssetDefaults` (in `Web/Common/Defaults/`):
```razor
<img src="@AssetDefaults.LogoAuth" />
<img src="@AssetDefaults.LogoSidebar" />
<div style="background-image: url('@AssetDefaults.BackgroundAuth')"></div>
```
- All logo and background image paths referenced through static properties.
- Single place to update when swapping branding assets.
