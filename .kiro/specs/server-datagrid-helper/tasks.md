# Implementation Plan

## Overview

Add a reusable Web-layer `ServerDataGridHelper` static helper with small composable methods (`ResolvePageSize`, `ExtractSort`, `ToGridData`, `Empty`) for database-backed `MudDataGrid` `ServerData` callbacks, refactor the audit log grid's `ServerReload` to compose them (fixing the latent line-numbering edge case), add unit tests, and reconcile steering. Behavior-preserving for the audit log grid; additive for future grids. The page keeps its own thin `ServerReload` (filters + service call + readiness guard + loading state inline), matching the existing `DataGridHelper<T>` usage pattern.

## Tasks

- [x] 1. Create the `ServerDataGridHelper` composable helpers in the Web project
  - Add `AspireWebAppTemplate.Web/Utilities/ServerDataGridHelper.cs` (namespace `AspireWebAppTemplate.Web.Utilities`) with static methods per design:
    - `int ResolvePageSize<T>(GridState<T> state, int defaultPageSize = 10)` — guards `PageSize <= 0`
    - `(string? SortBy, bool SortDescending) ExtractSort<T>(GridState<T> state)` — first sort definition or `(null, true)`
    - `GridData<TViewModel> ToGridData<TViewModel, TDto>(ApiResult<PagedResult<TDto>>? result, int page, int pageSize, Func<TDto,int,TViewModel> toViewModel)` — failure/null -> empty; success -> map with `page * pageSize + index + 1` line numbering
    - `GridData<TViewModel> Empty<TViewModel>()` — empty grid for readiness / no-data
  - No delegate-juggling, no readiness/loading/CancellationToken ownership in the helper (those stay in the page)
  - XML docs on the class and each method; present-tense comments
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 2.1, 2.2, 4.1, 4.2, 4.3, 4.4_

- [x] 2. Unit tests for the helpers
  - Add tests (Web test area) constructing `GridState<T>`/`SortDefinition<T>` and fake `ApiResult`/`PagedResult` values
  - Cover: `ResolvePageSize` (0 -> default, >0 -> passthrough); `ExtractSort` (with/without sort definition); `ToGridData` null/unsuccessful/null-data -> empty + projector not called; `ToGridData` success -> `TotalItems == TotalCount` + mapped view-models; line-number correctness including the resolved-page-size case from `PageSize=0, Page>0`
  - _Requirements: 1.2, 1.3, 1.5, 1.6, 1.7, 2.1, 2.2_

- [x] 3. Refactor the audit log ServerReload to compose the helpers
  - Update `Web/Components/Pages/Admin/AuditLog/Index.razor.cs` `ServerReload` to: return `ServerDataGridHelper.Empty<AuditLogViewModel>()` on the `_isReady` guard; keep the `IsLoading` `try/finally`; call `ResolvePageSize` and `ExtractSort`; build `AuditLogQueryParams` inline from `_searchString`/`_actionTypeFilter`/`_entityTypeFilter`/`_dateRange` + resolved sort/paging; call `AuditLogService.GetPagedAsync`; map the result with `ServerDataGridHelper.ToGridData(...)` and an `AuditLogViewModel` projector
  - Verify and preserve `_totalItems` usage (set from the returned `GridData.TotalItems` if still consumed)
  - The line-numbering fix is applied inherently (helper uses the resolved page size)
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 2.3_

- [x] 4. Checkpoint - build + full suite green
  - Build the solution; run the full test suite; confirm the audit log regression tests and the new helper tests pass, and no unrelated failures are attributable to this change (the Aspire `WebTests` startup smoke test is a known intermittent flake, not caused by this feature)
  - _Requirements: 3.3_

- [x] 5. Reconcile steering/docs
  - Update the "Data Grids" section in `.kiro/steering/template/ui-patterns.md` to name the `ServerDataGridHelper` helpers as the standard building blocks for database-backed grids' `ServerData` callback (alongside `DataGridHelper<T>` for in-memory)
  - Ensure `Web/Utilities/` is reflected where the Web project structure is described (structure.md / overview.md) if those enumerate Web utilities
  - _Requirements: 4.1_

## Notes

- Behavior-preserving for the audit log grid; additive for future grids.
- Composable methods (not one absorbing method) — the page keeps a readable `ServerReload` consistent with the in-memory `DataGridHelper<T>` usage.
- No changes to `QueryableExtensions` (Application) or `QueryablePagingExtensions` (Infrastructure); no generic DB filter engine; no multi-column sort; no conversion of in-memory grids.
- The helper lives in Web because it depends on MudBlazor `GridState`/`GridData` and on `ApiResult`/`PagedResult`; Application and Infrastructure remain free of MudBlazor, and the UI RCL keeps no `Application` reference.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1"] },
    { "id": 1, "tasks": ["2", "3"] },
    { "id": 2, "tasks": ["4"] },
    { "id": 3, "tasks": ["5"] }
  ]
}
```