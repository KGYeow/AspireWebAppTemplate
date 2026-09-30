# Requirements Document

## Introduction

This feature adds a small, reusable helper for the **database-level `MudDataGrid` `ServerData` callback** in the `AspireWebAppTemplate.Web` project. It is the UI-layer counterpart to the pieces the template already ships for the other layers:

- `DataGridHelper<T>` (in `UI/Utilities`) — the complete **in-memory** grid pipeline (filter + search + sort + paginate over `IEnumerable<T>`).
- `QueryableExtensions.ApplySort` (Application) — provider-agnostic dynamic sort composition.
- `QueryablePagingExtensions.ToPagedResultAsync` (Infrastructure) — EF Core count + sort + page + project to `PagedResult<T>`.

For a **database-backed** grid (large/unbounded datasets: the audit log today, business-specific reporting/log/transaction grids tomorrow), the page's `ServerData` callback is written by hand and repeats the same non-feature-specific ceremony every time: a prerender/ready guard, a page-size guard, single-column sort extraction from `GridState`, `ApiResult` failure handling, and mapping `PagedResult<TDto>` into `GridData<TViewModel>` with per-row line numbering. Only the filter mapping, DTO type, view-model type, and the service call are genuinely feature-specific.

Because `AspireWebAppTemplate` is a **template** whose explicit purpose is to be copied into business applications, a business app adding a second database-backed grid is a predictable, intended use — not speculative. This feature therefore extracts the repeated ceremony into a reusable Web-layer helper so a new database-backed grid's `ServerData` callback contains only its feature-specific parts, while keeping the existing behavior of the audit log grid unchanged.

The change is behavior-preserving for the audit log grid, additive elsewhere, and includes fixing a latent line-numbering edge case discovered during analysis.

### Current-state grounding (verified in the codebase)

- The audit log page `Web/Components/Pages/Admin/AuditLog/Index.razor.cs` has a hand-written `ServerReload(GridState<AuditLogViewModel> state, CancellationToken)` (~50 lines) that: guards on `_isReady`; sets `IsLoading`; computes `pageSize = state.PageSize > 0 ? state.PageSize : 10`; extracts single-column sort via `state.SortDefinitions.FirstOrDefault()`; builds an `AuditLogQueryParams` DTO from grid state plus toolbar filter fields (`_searchString`, `_actionTypeFilter`, `_entityTypeFilter`, `_dateRange`); calls `AuditLogService.GetPagedAsync`; on `!Succeeded || Data is null` returns an empty `GridData`; maps `PagedResult<AuditLogEntryDto>.Items` into `AuditLogViewModel` with a `LineNumber`; returns `GridData<AuditLogViewModel>`.
- **Latent line-numbering bug:** the query uses the guarded local `pageSize`, but the line-number offset is computed as `state.Page * state.PageSize` (the raw, unguarded value). When `state.PageSize == 0` (pre-init) and `state.Page > 0`, the offset collapses to 0 and line numbers ignore the page offset. Data is correct (the service paged with the guarded value); only the displayed line number is affected.
- The audit log is currently the **only** database-level grid. All other admin grids (roles, users, announcements, email templates) use `DataGridHelper<T>` (in-memory) and are out of scope.
- No `QueryableDataGridHelper<T>` type exists, despite references in `README.md`, `docs/architecture/overview.md`, and steering (those references were previously corrected to describe the real mechanism).
- The helper must consume MudBlazor types (`GridState<T>`, `GridData<T>`, `SortDefinition<T>`), which exist only in the Web/UI layer. `PagedResult<T>` lives in `Application/Common`; `ApiResult<T>` lives in `Application/Common`.

## Glossary

- **Web_Project**: The `AspireWebAppTemplate.Web` Blazor Server project.
- **ServerData_Callback**: The `Func<GridState<T>, Task<GridData<T>>>` (with `CancellationToken`) bound to a `MudDataGrid<T>` `ServerData` parameter that loads one page of data on demand.
- **ServerDataGrid_Helper**: The new static Web_Project helper class (`ServerDataGridHelper`) introduced by this feature, exposing small composable methods a page's `ServerData` callback calls for the mechanical ceremony of a database-backed grid: `ResolvePageSize`, `ExtractSort`, `ToGridData`, and `Empty`.
- **ViewModel_Projector**: A per-feature callback the page passes to `ToGridData` that maps a `TDto` plus its 1-based line number to a `TViewModel`.
- **ServerReload_Method**: The page-owned `ServerData` callback method (e.g. the audit log page's `ServerReload`) that keeps the feature-specific filter mapping, service call, readiness guard, and loading-state handling, and calls the ServerDataGrid_Helper methods for the mechanical parts.
- **PagedResult / PagedResult&lt;T&gt;**: The paged wrapper in `Application/Common/PagedResult.cs` (`Items`, `TotalCount`, `Page`, `PageSize`).
- **ApiResult / ApiResult&lt;T&gt;**: The Web-side result wrapper in `Application/Common/ApiResult.cs` (`Succeeded`, `Error`, `Data`).
- **Line_Number**: A per-row, 1-based sequential display index accounting for the current page offset (`page * pageSize + rowIndex + 1`).

## Requirements

### Requirement 1: Reusable composable database-level ServerData helpers

**User Story:** As a template developer building a new database-backed grid, I want small reusable helper methods for the mechanical parts of the `ServerData` callback, so that my page keeps its own thin `ServerReload` (with feature-specific filters and service call inline) and calls the helpers for the boilerplate — matching the existing in-memory `DataGridHelper<T>` ergonomics.

#### Acceptance Criteria

1. THE Web_Project SHALL provide a ServerDataGrid_Helper static class whose methods a page's ServerReload_Method composes; no single method SHALL absorb the page's whole `ServerData` flow.
2. THE ServerDataGrid_Helper SHALL provide an `ExtractSort` method that returns single-column sort `(SortBy, SortDescending)` from `GridState.SortDefinitions.FirstOrDefault()`; WHEN no sort definition is present it SHALL return a null sort property name (so the feature service applies its default sort) and `true` for descending.
3. THE ServerDataGrid_Helper SHALL provide a `ResolvePageSize` method that returns `GridState.PageSize` when it is greater than 0, and a caller-provided default page size otherwise.
4. THE ServerDataGrid_Helper SHALL provide a `ToGridData` method that accepts an `ApiResult<PagedResult<TDto>>`, the page index, the resolved page size, and a ViewModel_Projector, and returns a `GridData<TViewModel>`.
5. WHEN the `ApiResult` passed to `ToGridData` is null, unsuccessful, OR has null `Data`, THE `ToGridData` method SHALL return an empty `GridData<TViewModel>` with `TotalItems = 0` and SHALL NOT invoke the ViewModel_Projector.
6. WHEN the `ApiResult` passed to `ToGridData` is successful, THE method SHALL map each `TDto` to a `TViewModel` via the ViewModel_Projector and return a `GridData<TViewModel>` whose `TotalItems` equals the `PagedResult.TotalCount`.
7. THE `ToGridData` method SHALL compute each row's Line_Number as `pageSize * pageIndex + rowIndex + 1` using the resolved page size supplied by the caller, so line numbers remain correct across pages regardless of the raw `GridState.PageSize` value.
8. THE ServerDataGrid_Helper SHALL provide an `Empty` method returning an empty `GridData<TViewModel>` (`Items` empty, `TotalItems = 0`) for the page to return on its own readiness guard or other no-data cases.
9. THE page's ServerReload_Method SHALL retain ownership of the readiness guard, loading-state (`IsLoading`) handling, and `CancellationToken` usage; the ServerDataGrid_Helper methods SHALL NOT manage these.

### Requirement 2: Correct line numbering

**User Story:** As a user viewing a paged database-backed grid, I want row line numbers to reflect the current page offset, so that the numbering is continuous and correct on every page.

#### Acceptance Criteria

1. THE `ToGridData` method SHALL compute Line_Number as `resolvedPageSize * pageIndex + rowIndex + 1` using the resolved page size passed by the caller (obtained from `ResolvePageSize`).
2. WHEN `GridState.PageSize` is 0 (pre-initialization) and the page index is greater than 0, THE computed Line_Number SHALL still account for the page offset using the resolved page size.
3. THE audit log grid SHALL, after refactoring, produce identical line numbers to a correct manual computation for all steady-state (non-pre-init) cases.

### Requirement 3: Audit log refactor (behavior-preserving)

**User Story:** As a template maintainer, I want the existing audit log grid refactored to use the ServerDataGrid_Helper methods, so that the template demonstrates the reusable pattern and the audit log stops carrying duplicated ceremony while keeping a readable `ServerReload`.

#### Acceptance Criteria

1. THE audit log page's ServerReload_Method SHALL use `ResolvePageSize`, `ExtractSort`, and `ToGridData` (and `Empty` for its readiness guard) instead of duplicating that ceremony inline.
2. THE audit log page SHALL retain its feature-specific behavior inline: building `AuditLogQueryParams` from its toolbar filter fields, calling `AuditLogService.GetPagedAsync`, and mapping `AuditLogEntryDto` to `AuditLogViewModel`.
3. THE refactored audit log grid SHALL preserve existing observable behavior: same filtering, same sorting, same pagination, same loading indicator behavior, and same empty-on-failure behavior.
4. THE refactored audit log page SHALL preserve its readiness guard (no data fetch during prerender / before the timezone context is initialized), owned by the page.

### Requirement 4: Correct layering and reuse boundaries

**User Story:** As an architect, I want the helper placed so it does not violate the template's layer boundaries, so that Application and Infrastructure remain free of UI/MudBlazor dependencies.

#### Acceptance Criteria

1. THE ServerDataGrid_Helper SHALL reside in the Web_Project, because it depends on MudBlazor `GridState`/`GridData` types AND on `ApiResult`/`PagedResult`; it SHALL NOT be placed in the UI Razor Class Library (which currently has no `Application` reference).
2. THE ServerDataGrid_Helper SHALL NOT introduce any MudBlazor dependency into the `Application` or `Infrastructure` projects.
3. THE ServerDataGrid_Helper SHALL depend only on already-referenced types: MudBlazor grid types, `PagedResult<T>`, and `ApiResult<T>`.
4. THE feature-specific filter construction and service invocation SHALL remain in the page's ServerReload_Method, NOT inside the ServerDataGrid_Helper.

## Cross-Cutting Concerns

### Regression safety
- The audit log grid's observable behavior MUST remain equivalent after the refactor; existing audit log tests act as the regression guard, supplemented by new tests for the helper.

### Coding standards
- The helper MUST follow template conventions (XML docs on public members, present-tense comments, traditional structure) consistent with the existing `DataGridHelper<T>`.

### Non-goals
- This feature does NOT add a generic database-level filtering engine (per-feature `Where`/DTO filters remain explicit).
- This feature does NOT convert any existing in-memory grid (roles, users, announcements, email templates) to the database-level pattern.
- This feature does NOT add multi-column sort to the database-level path (the current DB path is single-column; multi-sort is a possible future extension).
- This feature does NOT modify `QueryableExtensions` (Application) or `QueryablePagingExtensions` (Infrastructure).