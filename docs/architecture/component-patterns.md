# Component Patterns

## MudDataGrid with the ServerData callback

All admin grids use `MudDataGrid<T>` with the `ServerData` callback (never the `Items` binding). Two
mechanisms back that callback; choose based on dataset size and where the data lives (see the "Data Grids"
section in `steering/template/ui-patterns.md` for the decision rule):

### In-memory: `DataGridHelper<T>`

Located at `AspireWebAppTemplate.UI/Utilities/DataGridHelper.cs`. The page loads the **full** set from
its service and `DataGridHelper<T>` applies filtering, search, sorting, and pagination **in application
memory** over `IEnumerable<T>`. Correct for small, bounded lists (roles, users, announcements, email
templates) or when filtering/sorting depends on computed view-model fields that cannot translate to SQL.

Provides:
- In-memory filtering (global search + per-column) over the materialized list
- Multi-column sorting
- Pagination with page-aware line numbering
- Entry point: `Task<GridData<T>> ServerReloadAsync(GridState<T> state, ...)`

### Database-level: `ServerDataGridHelper` + service + `ToPagedResultAsync`

For large/unbounded datasets (the audit log; future reporting/log grids), the page maps `GridState`
into a query-param DTO and calls a feature service that composes the query on `IQueryable` and returns
a `PagedResult<T>` — so filtering/sorting/paging execute in the database and only one page is
materialized. The pieces:

- `ServerDataGridHelper` (`AspireWebAppTemplate.Web/Utilities/`) — composable `ServerData` helpers the
  page calls: `ResolvePageSize`, `ExtractSort`, `ToGridData` (PagedResult → GridData with page-aware
  line numbering), and `Empty` for the readiness guard. The page keeps its filters, service call,
  readiness guard, and loading state inline.
- `QueryablePagingExtensions.ToPagedResultAsync` (`AspireWebAppTemplate.Infrastructure/Extensions/`) —
  EF Core count + sort + page + project → `PagedResult<T>`.
- `QueryableExtensions.ApplySort` (`AspireWebAppTemplate.Application/Extensions/`) — provider-agnostic
  dynamic sort composition.

Filtering on the database path is written per feature as explicit `Where` clauses in the service (there
is deliberately no generic filter engine).

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
