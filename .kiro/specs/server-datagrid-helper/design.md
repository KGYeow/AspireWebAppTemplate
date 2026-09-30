# Design Document

## Overview

Add a small set of reusable **composable helper methods** in the Web layer that handle the mechanical parts of a database-level `MudDataGrid` `ServerData` callback, and refactor the audit log grid to use them. Each page keeps its own thin `ServerReload` method (building its own filters and calling its own service inline), and calls the helper methods for the boilerplate — mirroring how in-memory pages keep a `ServerReload` that delegates the mechanical work to `DataGridHelper<T>`.

The helper is the database-level sibling of the existing pieces:

| Layer | Existing piece | Responsibility |
|---|---|---|
| Application | `QueryableExtensions.ApplySort` | provider-agnostic dynamic sort composition |
| Infrastructure | `QueryablePagingExtensions.ToPagedResultAsync` | EF Core count + sort + page + project -> `PagedResult<T>` |
| UI/Web (in-memory) | `DataGridHelper<T>.ServerReloadAsync` | complete in-memory filter/search/sort/paginate over `IEnumerable<T>` |
| **UI/Web (database-level)** | **`ServerDataGridHelper` (NEW)** | **small composable helpers a page's `ServerReload` calls: page-size guard, single-column sort extraction, and `PagedResult<TDto>` -> `GridData<TViewModel>` mapping with line numbering** |

### Why composable methods rather than one method

An earlier design used a single `LoadAsync(state, fetchDelegate, projector, isReady, defaultPageSize, ct)` that absorbed the whole flow. That was rejected because (a) it had too many parameters, and (b) it did not match the existing pattern — every page in the codebase keeps its own readable `ServerReload`, and the in-memory `DataGridHelper<T>` is a helper the page *calls*, not a method that swallows the page's control flow. The revised design provides small, focused helpers the page composes, keeping the page's filter mapping, service call, readiness guard, and loading-state handling visible in `ServerReload` exactly like the other pages.

## Architecture

### Placement and layering

- File: `AspireWebAppTemplate.Web/Utilities/ServerDataGridHelper.cs` (new `Utilities/` folder in Web, mirroring `UI/Utilities` and `Application/Utilities`). It depends on MudBlazor `GridState`/`GridData`, so it cannot live in Application/Infrastructure; it is not a Razor component, so `Utilities` fits better than `Components`.
- Namespace: `AspireWebAppTemplate.Web.Utilities`.
- Dependencies: MudBlazor grid types, `Application.Common.ApiResult<T>`, `Application.Common.PagedResult<T>` — all already referenced by Web.
- Boundary rule: no MudBlazor type leaks into Application or Infrastructure. The DataGrid->query translation stays in the Web page, never inside Application/Infrastructure.
- Placement decision (confirmed): kept in **Web**, not the UI RCL. The UI project references neither `Application` nor `ApiResult`/`PagedResult` today; because these helpers consume `ApiResult<PagedResult<TDto>>`, placing them in UI would force a first-ever UI->Application coupling. The in-memory `DataGridHelper<T>` belongs in UI precisely because it has no such dependency.

### Data flow

```
MudDataGrid ServerData(GridState, ct)   // page keeps its own ServerReload
        v
page: readiness guard, IsLoading try/finally         (stays in page)
page: ServerDataGridHelper.ResolvePageSize(state)          (helper)
page: ServerDataGridHelper.ExtractSort(state)              (helper)
page: build query params + call service inline       (feature-specific, stays in page)
        v
ApiResult<PagedResult<TDto>>
        v
page: ServerDataGridHelper.ToGridData(apiResult, page, pageSize, projector)   (helper: failure->empty, map + line numbers)
        v
GridData<TViewModel>
```

### Alternatives considered

- **Single all-in-one `LoadAsync` delegate method.** Rejected: too many parameters and inconsistent with the existing `ServerReload`/`DataGridHelper<T>` pattern (see "Why composable methods" above).
- **Instance-based helper configured with fluent `Map*` calls (like `DataGridHelper<T>`).** Rejected: the in-memory helper needs per-column selectors because it filters/sorts in memory; the database-level path filters/sorts in SQL inside the service, so there is nothing to configure per column — static composable methods are sufficient and simpler.
- **Placing the helper in `Application`/`Infrastructure`.** Rejected: it consumes MudBlazor `GridState`/`GridData`.
- **Placing it in the `AspireWebAppTemplate.UI` RCL.** Rejected: would force a UI->Application reference (see Placement decision).
- **Owning `IsLoading` or the readiness guard inside the helper.** Rejected: those stay visible in the page's `ServerReload`, consistent with every other page.
- **Multi-column sort.** Deferred: the DB path and `AuditLogQueryParams` are single-sort; out of scope.

## Components and Interfaces

A static class exposing small, focused methods. The page composes them inside its own `ServerReload`.

```csharp
namespace AspireWebAppTemplate.Web.Utilities;

/// <summary>
/// Small composable helpers for a database-backed <see cref="MudDataGrid{T}"/> ServerData callback.
/// A page's ServerReload calls these for the mechanical parts (page-size guard, single-column sort
/// extraction, and PagedResult -> GridData mapping with line numbering) while keeping its own
/// filter mapping, service call, readiness guard, and loading-state handling inline.
/// </summary>
public static class ServerDataGridHelper
{
    /// <summary>
    /// Resolves the effective page size, substituting <paramref name="defaultPageSize"/> when the grid
    /// has not yet initialized its own page size (<c>state.PageSize &lt;= 0</c>).
    /// </summary>
    public static int ResolvePageSize<T>(GridState<T> state, int defaultPageSize = 10)
        => state.PageSize > 0 ? state.PageSize : defaultPageSize;

    /// <summary>
    /// Extracts single-column sort from the grid state. Returns a null property name when no sort is
    /// applied, letting the feature service fall back to its own default ordering.
    /// </summary>
    public static (string? SortBy, bool SortDescending) ExtractSort<T>(GridState<T> state)
    {
        var first = state.SortDefinitions.FirstOrDefault();
        return first is not null && !string.IsNullOrWhiteSpace(first.SortBy)
            ? (first.SortBy, first.Descending)
            : (null, true);
    }

    /// <summary>
    /// Maps a paged service result into <see cref="GridData{TViewModel}"/>. On an unsuccessful or null
    /// result, returns an empty grid. On success, projects each DTO to a view-model with a 1-based,
    /// page-aware line number computed from the resolved page size.
    /// </summary>
    public static GridData<TViewModel> ToGridData<TViewModel, TDto>(
        ApiResult<PagedResult<TDto>>? result,
        int page,
        int pageSize,
        Func<TDto, int, TViewModel> toViewModel)
    {
        if (result is null || !result.Succeeded || result.Data is null)
            return Empty<TViewModel>();

        var paged = result.Data;
        var items = paged.Items
            .Select((dto, index) => toViewModel(dto, page * pageSize + index + 1))
            .ToList();

        return new GridData<TViewModel> { Items = items, TotalItems = paged.TotalCount };
    }

    /// <summary>
    /// Returns an empty <see cref="GridData{TViewModel}"/> (no items, zero total) for the not-ready
    /// or no-data cases.
    /// </summary>
    public static GridData<TViewModel> Empty<TViewModel>()
        => new() { Items = [], TotalItems = 0 };
}
```

Notes:
- Each method is tiny with 1-3 obvious parameters — no delegate-juggling and no 6-argument signature.
- **Line-number fix** lives in `ToGridData`: `page * pageSize + index + 1` uses the resolved page size (the caller passes the guarded value), fixing the latent bug where the audit log used the raw `state.PageSize`.
- The page still owns readiness (`Empty<T>()` on not-ready), `IsLoading`, filters, and the service call.

### Refactored audit log ServerReload

```csharp
private async Task<GridData<AuditLogViewModel>> ServerReload(
    GridState<AuditLogViewModel> state, CancellationToken cancellationToken)
{
    if (!_isReady)
        return ServerDataGridHelper.Empty<AuditLogViewModel>();

    IsLoading = true;
    try
    {
        var pageSize = ServerDataGridHelper.ResolvePageSize(state);
        var (sortBy, sortDescending) = ServerDataGridHelper.ExtractSort(state);

        var result = await AuditLogService.GetPagedAsync(new AuditLogQueryParams
        {
            Page = state.Page,
            PageSize = pageSize,
            SearchTerm = _searchString,
            ActionType = _actionTypeFilter,
            EntityType = _entityTypeFilter,
            DateStart = TimeZoneContext.ConvertToUtc(_dateRange?.Start),
            DateEnd = TimeZoneContext.ConvertToUtc(_dateRange?.End?.Date.AddDays(1).AddTicks(-1)),
            SortBy = sortBy,
            SortDescending = sortDescending
        });

        var grid = ServerDataGridHelper.ToGridData(
            result, state.Page, pageSize,
            (entry, lineNumber) => new AuditLogViewModel { LineNumber = lineNumber, Entry = entry });

        _totalItems = grid.TotalItems;   // preserve only if still consumed elsewhere
        return grid;
    }
    finally
    {
        IsLoading = false;
    }
}
```

This reads like the other `ServerReload` methods: readiness guard + `try/finally` visible in the page, filters inline, service call inline, and the mechanical bits delegated to small helpers.

## Data Models

This feature introduces **no new data models**. It reuses existing types:

- `PagedResult<T>` (`Application/Common/PagedResult.cs`) — `Items`, `TotalCount`, `Page`, `PageSize`.
- `ApiResult<T>` (`Application/Common/ApiResult.cs`) — `Succeeded`, `Error`, `Data`.
- MudBlazor `GridState<T>` (input: `Page`, `PageSize`, `SortDefinitions`) and `GridData<T>` (output: `Items`, `TotalItems`).
- `AuditLogQueryParams`, `AuditLogEntryDto`, `AuditLogViewModel` — unchanged; used only by the audit log call site.

The only new artifact is the static `ServerDataGridHelper` class and its methods.

## Error Handling

- **Fetch failure / null data:** WHEN the `ApiResult` passed to `ToGridData` is null, unsuccessful, or has null `Data`, it returns an empty `GridData<TViewModel>` (`TotalItems = 0`) and does not throw — matching the audit log's current empty-on-failure behavior. Error surfacing (snackbar/alert) remains the page's responsibility.
- **Not ready / prerender:** the page calls `ServerDataGridHelper.Empty<T>()` on its own `_isReady` guard before fetching — the guard stays in the page, preserving the audit log's prerender behavior.
- **Loading state / exceptions:** the page keeps its `try/finally` around the fetch; the helpers do not swallow exceptions or manage `IsLoading`.

## Correctness Properties

### Property 1: Page-size guard
For any `state` with `PageSize <= 0`, `ResolvePageSize` returns `defaultPageSize`; otherwise it returns `state.PageSize`.
**Validates: Requirements 1.3, 2.1**

### Property 2: Line-number continuity
For any page index and resolved page size, `ToGridData` numbers row `index` as `pageSize * page + index + 1`, independent of the raw `state.PageSize`.
**Validates: Requirements 1.7, 2.1, 2.2**

### Property 3: Failure closure
For any null/unsuccessful/null-data `ApiResult`, `ToGridData` returns an empty grid and never invokes the projector.
**Validates: Requirements 1.5**

### Property 4: Total fidelity
On a successful result, `ToGridData` returns `GridData.TotalItems` equal to `PagedResult.TotalCount`.
**Validates: Requirements 1.6**

### Property 5: Sort extraction
`ExtractSort` returns the first sort definition's `(SortBy, Descending)` when present and non-empty; otherwise `(null, true)`.
**Validates: Requirements 1.2**

## Testing Strategy

- **Unit tests** (Web test area) for the `ServerDataGridHelper` methods, constructing `GridState<T>`/`SortDefinition<T>` and fake `ApiResult`/`PagedResult` values:
  - `ResolvePageSize`: `PageSize = 0` -> default; `PageSize > 0` -> passthrough (Property 1).
  - `ExtractSort`: with / without a sort definition -> correct tuple (Property 5).
  - `ToGridData`: null/unsuccessful/null-data -> empty, projector not called (Property 3); success -> `TotalItems == TotalCount` (Property 4); line-number correctness including `pageSize` from the `PageSize=0, Page>0` resolved case (Property 2).
- **Regression:** existing `AuditLogQueryServiceTests` continue to pass (service unchanged); audit log page verified by build + existing suite.

## Steering Reconciliation

Update the "Data Grids" guidance in `ui-patterns.md` to name the `ServerDataGridHelper` helpers as the standard building blocks for database-backed grids' `ServerData` callback (alongside `DataGridHelper<T>` for in-memory), so future business features discover them.