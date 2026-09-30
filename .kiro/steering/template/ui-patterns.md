---
inclusion: always
---

<!-- TEMPLATE-OWNED steering (UI / MudBlazor conventions). Inherited from AspireWebAppTemplate.
     Business apps should KEEP these conventions; pull updates from the template repo (steering/template/*).
     See steering/template/template-guardrails.md for the capability-protection rule. -->

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
- ALL admin DataGrid pages use `MudDataGrid<T>` with a `ServerData` callback (never the `Items` binding).
- Always include `<NoRecordsContent>` and `<LoadingContent>`.
- `Items` binding is reserved for truly static lists (e.g., settings dropdowns, enum selectors) - NOT for admin management grids.

#### Choosing in-memory vs database-level processing
Two mechanisms back the `ServerData` callback. Choose based on dataset size and where the data lives; this choice has real scalability impact, so make it deliberately:

- **In-memory (default for small, bounded lists):** use `DataGridHelper<T>` (`UI/Utilities/DataGridHelper.cs`). The page loads the full set from its service (e.g. `GetAllAsync`) and `DataGridHelper<T>` applies column filters, global search, multi-sort, and pagination in application memory over `IEnumerable<T>` via fluent `Map*` selectors. Used by roles, users, announcements, and email templates. Correct when the dataset is small and fully materialized, or when filtering/sorting depends on computed view-model fields that cannot translate to SQL.
- **Database-level (required for large/unbounded datasets):** the page maps the grid `GridState` (page, page size, `SortDefinitions`, toolbar filters) into a query-param DTO (e.g. `AuditLogQueryParams` with `SortBy`/`SortDescending`) and calls a feature service that composes the query on `IQueryable` and returns a `PagedResult<T>`. The service applies filters as `Where` clauses, sorts via `QueryableExtensions.ApplySort` (`Application/Extensions/`), then `CountAsync` + `Skip`/`Take`/`Select`/`ToListAsync` (via `QueryablePagingExtensions.ToPagedResultAsync` in `Infrastructure/Extensions/`) so the database does the work and only one page is materialized. Used by the audit log. On the page side, the `ServerData` callback uses `ServerDataGridHelper` (`Web/Utilities/ServerDataGridHelper.cs`) for the mechanical ceremony — `ResolvePageSize`, `ExtractSort`, and `ToGridData` (PagedResult -> GridData with page-aware line numbering), plus `Empty` for the readiness guard — while the page keeps its filters, service call, readiness guard, and loading state inline. See "Server-Side Sorting & Pagination (Large Datasets)" in `coding-standards.md` for the data-access details.
- **Avoid:** loading a large table with `GetAllAsync` and handing it to `DataGridHelper<T>` - that pulls the whole table into memory and defeats database paging.
- The database-level pieces are: `ServerDataGridHelper` (Web, `ServerData` ceremony), `QueryablePagingExtensions.ToPagedResultAsync` (Infrastructure, count/sort/page/project), and `QueryableExtensions.ApplySort` (Application, provider-agnostic sort). **Filtering is still per-feature** — each database-level service writes its own `Where` clauses (there is deliberately no generic filter engine). If several database-backed grids end up sharing filter shapes, consider extracting a shared queryable filter helper then (not before).

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

## DataGrid ViewModel Pattern

All admin DataGrid pages use a wrapper ViewModel that holds the DTO reference:
```razor
private sealed class ItemViewModel
{
    public int LineNumber { get; set; }
    public ItemDto Item { get; set; } = default!;
    public string Id => Item.Id;
    // ... only properties consumed by the view
    public override bool Equals(object? obj) => obj is ItemViewModel other && Id == other.Id;
    public override int GetHashCode() => Id.GetHashCode();
}
```
- Mapping: `new ItemViewModel { Item = dto }`
- `Equals`/`GetHashCode` required for multi-selection pages
- Computed properties (not on DTO) stay as settable fields on the ViewModel
- **Only delegate properties that the view actually consumes** (grid columns, event handlers, filters). Do NOT mirror every DTO property — the underlying DTO is accessible via `vm.Item` when needed (e.g., passing to dialogs).

## Enum Column Filtering

For enum property columns, use `EnumFilterSelect` with `MapEnum` in DataGridHelper:
```razor
<PropertyColumn Property="t => t.Category" Title="Category" Filterable="true">
    <FilterTemplate>
        <EnumFilterSelect T="ItemViewModel" TEnum="MyEnum" FilterContext="context" DataGrid="_dataGrid" />
    </FilterTemplate>
</PropertyColumn>
```
- DataGridHelper mapping: `.MapEnum(nameof(ItemViewModel.Category), x => x.Category)`
- Filter select components (`BoolFilterSelect`, `EnumFilterSelect`, `StringFilterSelect`) have nullable `DataGrid` parameter with null guard for safe initialization
