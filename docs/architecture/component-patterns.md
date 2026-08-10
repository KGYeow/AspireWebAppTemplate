# Component Patterns

## MudDataGrid with Server-Side Data

All admin pages use `MudDataGrid<T>` with the `ServerData` callback pattern via `DataGridHelper<T>`.

### DataGridHelper<T>

Located at `AspireWebAppTemplate.UI/Utilities/DataGridHelper.cs`.

Provides:
- Server-side filtering (global search + per-column)
- Sorting (single column, ascending/descending)
- Pagination with page-aware line numbering
- Type: `Task<GridData<T>> ServerReloadAsync(GridState<T> state, ...)`

### Page Structure

```
Components/Pages/{Feature}/
├── Index.razor          (main page with MudDataGrid)
├── Index.razor.cs       (code-behind: services, state, ServerReload)
├── Details.razor        (detail view page, optional)
├── Details.razor.cs
├── AddDialog.razor      (creation dialog)
└── EditDialog.razor     (editing dialog)
```

### Toolbar Pattern

- Search field with debounce (500ms)
- Dropdown filters (BoolFilterSelect, enum selects)
- Bulk action buttons (shown only when rows selected)
- Export button

### Row Actions Pattern

- 2 or fewer actions: Direct buttons
- 3+ actions: Overflow menu (`MudMenu` with `MudMenuItem`)
- Always include "View Details" as first action

## View/Edit Mode Toggle (Profile Page)

- Default: View Mode (read-only MudText values)
- Edit button transitions to Edit Mode (form inputs)
- Same MudPaper containers in both modes — no layout shift
- Cancel restores original values, Save persists

## Instant-Save Pattern (Settings Page)

- No Save button, no EditForm
- Each field has a backing property with setter that triggers async save
- Optimistic UI with revert-on-failure
- Previous value tracked for rollback
- Success/error alerts after each save

## Section Containers

- `MudPaper Class="pa-4 mb-4" Elevation="0"` for content sections
- `MudText Typo="Typo.h6" Class="mb-3"` for section headings
- No MudCard with non-zero elevation

## Form Fields

- Separate `<MudInputLabel>` above inputs (not built-in Label prop)
- `Variant.Outlined`, `Margin.Dense`, `Typo="Typo.body2"` on all inputs
- `Class="fw-bold"` on all MudInputLabel elements

## PillToggle Component

- Generic `PillToggle<T>` wrapping `MudToggleGroup<T>` with pill styling
- `PillToggleItem<T>` with circular buttons (36×36px)
- `Title` parameter renders as `title` + `aria-label`
