using AspireWebAppTemplate.Application.Common;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Utilities;

/// <summary>
/// Small composable helpers for a database-backed <see cref="MudDataGrid{T}"/> <c>ServerData</c> callback.
/// A page's ServerReload method calls these for the mechanical parts — page-size guard, single-column
/// sort extraction, and mapping a paged service result into <see cref="GridData{T}"/> with page-aware
/// line numbering — while keeping its own filter mapping, service call, readiness guard, and
/// loading-state handling inline.
/// </summary>
/// <remarks>
/// This is the database-level counterpart to the in-memory <c>DataGridHelper&lt;T&gt;</c>. It lives in the
/// Web project because it depends on MudBlazor grid types (<see cref="GridState{T}"/>/<see cref="GridData{T}"/>)
/// as well as the <see cref="ApiResult{T}"/> / <see cref="PagedResult{T}"/> shapes returned by the typed
/// API clients. Filtering and the service call stay in the page (the query is executed at the database
/// level inside the feature service), so these helpers intentionally do not own filtering, the DTO, the
/// view-model, the readiness guard, the loading indicator, or cancellation.
/// </remarks>
public static class ServerDataGridHelper
{
    /// <summary>
    /// Resolves the effective page size, substituting <paramref name="defaultPageSize"/> when the grid
    /// has not yet initialized its own page size (<c>state.PageSize &lt;= 0</c>, which can occur before
    /// the grid's first render).
    /// </summary>
    /// <typeparam name="T">The grid row type.</typeparam>
    /// <param name="state">The current grid state.</param>
    /// <param name="defaultPageSize">The page size to use when the grid has not set one yet.</param>
    /// <returns>The grid's page size when positive; otherwise <paramref name="defaultPageSize"/>.</returns>
    public static int ResolvePageSize<T>(GridState<T> state, int defaultPageSize = 10)
        => state.PageSize > 0 ? state.PageSize : defaultPageSize;

    /// <summary>
    /// Extracts single-column sort from the grid state. Returns a null property name when no sort is
    /// applied, letting the feature service fall back to its own default ordering.
    /// </summary>
    /// <typeparam name="T">The grid row type.</typeparam>
    /// <param name="state">The current grid state.</param>
    /// <returns>
    /// A tuple of the sort property name and descending flag. When no sort definition is present or its
    /// property name is empty, returns <c>(null, true)</c>.
    /// </returns>
    public static (string? SortBy, bool SortDescending) ExtractSort<T>(GridState<T> state)
    {
        var first = state.SortDefinitions.FirstOrDefault();
        return first is not null && !string.IsNullOrWhiteSpace(first.SortBy)
            ? (first.SortBy, first.Descending)
            : (null, true);
    }

    /// <summary>
    /// Maps a paged service result into <see cref="GridData{TViewModel}"/>. On a null, unsuccessful, or
    /// null-data result, returns an empty grid without invoking the projector. On success, projects each
    /// DTO to a view-model with a 1-based, page-aware line number computed from the resolved page size.
    /// </summary>
    /// <typeparam name="TViewModel">The grid row view-model type.</typeparam>
    /// <typeparam name="TDto">The DTO type returned by the feature service.</typeparam>
    /// <param name="result">The paged API result from the feature service.</param>
    /// <param name="page">The zero-based page index that was requested.</param>
    /// <param name="pageSize">The resolved page size (from <see cref="ResolvePageSize{T}"/>), used for the line-number offset.</param>
    /// <param name="toViewModel">Projects a DTO plus its 1-based line number to a view-model.</param>
    /// <returns>A populated <see cref="GridData{TViewModel}"/> on success, or an empty grid on failure.</returns>
    public static GridData<TViewModel> ToGridData<TViewModel, TDto>(
        ApiResult<PagedResult<TDto>>? result,
        int page,
        int pageSize,
        Func<TDto, int, TViewModel> toViewModel)
    {
        // Failure closure: any missing/unsuccessful/empty result yields an empty grid and never projects.
        if (result is null || !result.Succeeded || result.Data is null)
            return Empty<TViewModel>();

        var paged = result.Data;

        // Line number is page-aware and 1-based, using the resolved page size so numbering stays correct
        // across pages regardless of the raw GridState.PageSize value.
        var items = paged.Items
            .Select((dto, index) => toViewModel(dto, page * pageSize + index + 1))
            .ToList();

        return new GridData<TViewModel> { Items = items, TotalItems = paged.TotalCount };
    }

    /// <summary>
    /// Returns an empty <see cref="GridData{TViewModel}"/> (no items, zero total). Used by a page to
    /// short-circuit its ServerReload on the readiness guard or other no-data cases.
    /// </summary>
    /// <typeparam name="TViewModel">The grid row view-model type.</typeparam>
    /// <returns>An empty grid with <c>TotalItems = 0</c>.</returns>
    public static GridData<TViewModel> Empty<TViewModel>()
        => new() { Items = [], TotalItems = 0 };
}