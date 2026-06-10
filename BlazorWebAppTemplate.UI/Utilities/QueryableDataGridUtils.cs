using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace BlazorWebAppTemplate.UI.Utilities;

/// <summary>
/// Generic database-level server-side filtering, sorting, and pagination utility for MudBlazor <see cref="MudDataGrid{T}"/>.
/// Unlike <see cref="DataGridUtils{T}"/> which works on in-memory collections,
/// this utility translates <see cref="GridState{T}"/> into EF Core <see cref="IQueryable{T}"/> expressions
/// (WHERE / ORDER BY / SKIP / TAKE) so only the matching page leaves the database.
/// Designed for large-dataset scenarios such as audit logs where loading all records into memory is impractical.
/// </summary>
/// <typeparam name="T">The entity type displayed in the grid.</typeparam>
/// <remarks>
/// <para>Usage: create an instance, register property selectors via the fluent
/// <c>Map*</c> methods (using <c>Expression&lt;Func&lt;T, ...&gt;&gt;</c> for EF Core translation),
/// then call <see cref="ServerReloadAsync"/> as part of the <c>ServerData</c> callback
/// on <see cref="MudDataGrid{T}"/>.</para>
/// <para>Example:</para>
/// <code>
/// private readonly QueryableDataGridUtils&lt;AuditLogEntry&gt; _gridUtils = new QueryableDataGridUtils&lt;AuditLogEntry&gt;()
///     .MapString(nameof(AuditLogEntry.UserDisplayName), x =&gt; x.UserDisplayName)
///     .MapString(nameof(AuditLogEntry.Description), x =&gt; x.Description)
///     .MapDateTime(nameof(AuditLogEntry.Timestamp), x =&gt; x.Timestamp);
/// </code>
/// </remarks>
public sealed class QueryableDataGridUtils<T> where T : class
{
    #region Selector Registries

    // Expression-based selectors allow EF Core to translate property access into SQL.
    // Each dictionary maps a column's PropertyName to its corresponding expression selector.
    private readonly Dictionary<string, Expression<Func<T, string?>>> _stringSelectors = new();
    private readonly Dictionary<string, Expression<Func<T, int?>>> _intSelectors = new();
    private readonly Dictionary<string, Expression<Func<T, long?>>> _longSelectors = new();
    private readonly Dictionary<string, Expression<Func<T, decimal?>>> _decimalSelectors = new();
    private readonly Dictionary<string, Expression<Func<T, double?>>> _doubleSelectors = new();
    private readonly Dictionary<string, Expression<Func<T, DateTime?>>> _dateTimeSelectors = new();
    private readonly Dictionary<string, Expression<Func<T, bool?>>> _boolSelectors = new();

    // Track the first registered DateTime property name for default sort fallback.
    private string? _firstDateTimeProperty;

    // Explicit default sort configuration (overrides first-DateTime fallback when set).
    private string? _defaultSortProperty;
    private bool _defaultSortDescending = true;

    #endregion

    #region Mapping API

    /// <summary>
    /// Registers a string property expression for column filtering, global search, and sorting.
    /// Uses <c>Expression&lt;Func&lt;T, string?&gt;&gt;</c> so EF Core can translate to SQL.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c> (typically <c>nameof(T.Property)</c>).</param>
    /// <param name="selector">An expression that extracts the string value from an entity.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public QueryableDataGridUtils<T> MapString(string propertyName, Expression<Func<T, string?>> selector)
    {
        _stringSelectors[propertyName] = selector;
        return this;
    }

    /// <summary>
    /// Registers a nullable int property expression for column filtering and sorting.
    /// Uses <c>Expression&lt;Func&lt;T, int?&gt;&gt;</c> so EF Core can translate to SQL.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c>.</param>
    /// <param name="selector">An expression that extracts the nullable int value from an entity.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public QueryableDataGridUtils<T> MapInt(string propertyName, Expression<Func<T, int?>> selector)
    {
        _intSelectors[propertyName] = selector;
        return this;
    }

    /// <summary>
    /// Registers a non-nullable int property expression for column filtering and sorting.
    /// Wraps the selector as nullable internally for consistent filter handling.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c>.</param>
    /// <param name="selector">An expression that extracts the int value from an entity.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public QueryableDataGridUtils<T> MapInt(string propertyName, Expression<Func<T, int>> selector)
    {
        // Wrap non-nullable as nullable for consistent filter/sort handling
        var param = selector.Parameters[0];
        var body = Expression.Convert(selector.Body, typeof(int?));
        var nullableSelector = Expression.Lambda<Func<T, int?>>(body, param);
        _intSelectors[propertyName] = nullableSelector;
        return this;
    }

    /// <summary>
    /// Registers a nullable long property expression for column filtering and sorting.
    /// Uses <c>Expression&lt;Func&lt;T, long?&gt;&gt;</c> so EF Core can translate to SQL.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c>.</param>
    /// <param name="selector">An expression that extracts the nullable long value from an entity.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public QueryableDataGridUtils<T> MapLong(string propertyName, Expression<Func<T, long?>> selector)
    {
        _longSelectors[propertyName] = selector;
        return this;
    }

    /// <summary>
    /// Registers a nullable decimal property expression for column filtering and sorting.
    /// Uses <c>Expression&lt;Func&lt;T, decimal?&gt;&gt;</c> so EF Core can translate to SQL.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c>.</param>
    /// <param name="selector">An expression that extracts the nullable decimal value from an entity.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public QueryableDataGridUtils<T> MapDecimal(string propertyName, Expression<Func<T, decimal?>> selector)
    {
        _decimalSelectors[propertyName] = selector;
        return this;
    }

    /// <summary>
    /// Registers a nullable double property expression for column filtering and sorting.
    /// Uses <c>Expression&lt;Func&lt;T, double?&gt;&gt;</c> so EF Core can translate to SQL.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c>.</param>
    /// <param name="selector">An expression that extracts the nullable double value from an entity.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public QueryableDataGridUtils<T> MapDouble(string propertyName, Expression<Func<T, double?>> selector)
    {
        _doubleSelectors[propertyName] = selector;
        return this;
    }

    /// <summary>
    /// Registers a nullable DateTime property expression for column filtering, sorting, and default sort fallback.
    /// The first DateTime property registered becomes the default sort field (descending) when no explicit sort is provided
    /// and no <see cref="DefaultSort"/> has been configured.
    /// Uses <c>Expression&lt;Func&lt;T, DateTime?&gt;&gt;</c> so EF Core can translate to SQL.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c>.</param>
    /// <param name="selector">An expression that extracts the nullable DateTime value from an entity.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public QueryableDataGridUtils<T> MapDateTime(string propertyName, Expression<Func<T, DateTime?>> selector)
    {
        _dateTimeSelectors[propertyName] = selector;
        _firstDateTimeProperty ??= propertyName;
        return this;
    }

    /// <summary>
    /// Registers a nullable bool property expression for column filtering and sorting.
    /// Uses <c>Expression&lt;Func&lt;T, bool?&gt;&gt;</c> so EF Core can translate to SQL.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c>.</param>
    /// <param name="selector">An expression that extracts the nullable bool value from an entity.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public QueryableDataGridUtils<T> MapBool(string propertyName, Expression<Func<T, bool?>> selector)
    {
        _boolSelectors[propertyName] = selector;
        return this;
    }

    /// <summary>
    /// Configures an explicit default sort to use when the GridState has no sort definitions.
    /// Overrides the automatic fallback to the first registered DateTime property.
    /// </summary>
    /// <param name="propertyName">A previously registered property name to sort by.</param>
    /// <param name="descending">Whether to sort descending (default: true).</param>
    /// <returns>This instance for fluent chaining.</returns>
    public QueryableDataGridUtils<T> DefaultSort(string propertyName, bool descending = true)
    {
        _defaultSortProperty = propertyName;
        _defaultSortDescending = descending;
        return this;
    }

    #endregion

    #region Public Entry Points

    /// <summary>
    /// Full database-level pipeline: column filters → global search → sort → count → paginate → line numbering → <see cref="GridData{T}"/>.
    /// All operations are translated to SQL by EF Core for efficient execution on large datasets.
    /// </summary>
    /// <param name="queryable">The base <see cref="IQueryable{T}"/> (e.g., <c>dbContext.AuditLogEntries.AsQueryable()</c>).
    /// The caller may pre-filter this queryable with additional WHERE clauses (e.g., toolbar filters) before passing it in.</param>
    /// <param name="state">MudDataGrid <see cref="GridState{T}"/> containing page index, page size, column filter definitions, and sort definitions.</param>
    /// <param name="globalSearchTerm">Optional global search term applied as OR across the fields specified in <paramref name="globalSearchFields"/>.
    /// Case-insensitive matching is performed via <c>EF.Functions.Like</c> or <c>ToLower().Contains()</c>.</param>
    /// <param name="globalSearchFields">Which registered string property names to include in the global search OR clause.
    /// If null or empty, global search is not applied even when <paramref name="globalSearchTerm"/> has a value.</param>
    /// <param name="setLineNumber">Optional callback to set a display line number on each item in the current page.
    /// Receives the entity and its 1-based line number (page-aware: page 0, size 10 → lines 1–10; page 1 → lines 11–20).</param>
    /// <param name="cancellationToken">Cancellation token for async database operations.</param>
    /// <returns>A <see cref="GridData{T}"/> containing the paged items and the total count of matching entries.</returns>
    public async Task<GridData<T>> ServerReloadAsync(
        IQueryable<T> queryable,
        GridState<T> state,
        string? globalSearchTerm = null,
        IEnumerable<string>? globalSearchFields = null,
        Action<T, int>? setLineNumber = null,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Apply column filters from GridState (translates to WHERE clauses)
        var filtered = ApplyColumnFilters(queryable, state.FilterDefinitions);

        // Step 2: Apply global search across specified string fields (OR condition)
        filtered = ApplyGlobalSearch(filtered, globalSearchTerm, globalSearchFields);

        // Step 3: Apply sort definitions or fall back to default sort (Timestamp DESC)
        var sorted = ApplySorting(filtered, state.SortDefinitions);

        // Step 4: Count total matching records (executes COUNT(*) in SQL)
        var totalCount = await sorted.CountAsync(cancellationToken);

        // Step 5: Apply pagination via SKIP/TAKE
        var pageItems = await sorted
            .Skip(state.Page * state.PageSize)
            .Take(state.PageSize)
            .ToListAsync(cancellationToken);

        // Step 6: Apply 1-based, page-aware line numbering if a callback is provided
        if (setLineNumber is not null)
        {
            for (var i = 0; i < pageItems.Count; i++)
            {
                // Line number accounts for the current page offset: page 0 size 10 → 1..10, page 1 → 11..20
                setLineNumber(pageItems[i], state.Page * state.PageSize + i + 1);
            }
        }

        return new GridData<T> { Items = pageItems, TotalItems = totalCount };
    }

    /// <summary>
    /// Returns all matching entries (up to <paramref name="maxRows"/>) for export scenarios.
    /// Applies column filters and global search but no pagination, capped at a configurable maximum
    /// to prevent memory exhaustion on very large datasets.
    /// </summary>
    /// <param name="queryable">The base <see cref="IQueryable{T}"/>. The caller may pre-filter before passing.</param>
    /// <param name="state">MudDataGrid <see cref="GridState{T}"/> containing column filter definitions (pagination is ignored).</param>
    /// <param name="globalSearchTerm">Optional global search term applied across specified string fields.</param>
    /// <param name="globalSearchFields">Which registered string property names to include in global search.</param>
    /// <param name="maxRows">Maximum number of rows to return. Defaults to 50,000 to prevent excessive memory usage and download sizes.</param>
    /// <param name="cancellationToken">Cancellation token for async database operations.</param>
    /// <returns>A list of all matching entities, up to <paramref name="maxRows"/>.</returns>
    public async Task<List<T>> GetAllMatchingAsync(
        IQueryable<T> queryable,
        GridState<T> state,
        string? globalSearchTerm = null,
        IEnumerable<string>? globalSearchFields = null,
        int maxRows = 50_000,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Apply column filters (same as ServerReloadAsync)
        var filtered = ApplyColumnFilters(queryable, state.FilterDefinitions);

        // Step 2: Apply global search (same as ServerReloadAsync)
        filtered = ApplyGlobalSearch(filtered, globalSearchTerm, globalSearchFields);

        // Step 3: Apply sorting for consistent ordering in exports
        var sorted = ApplySorting(filtered, state.SortDefinitions);

        // Step 4: Cap at maxRows to prevent memory exhaustion, then materialize
        return await sorted
            .Take(maxRows)
            .ToListAsync(cancellationToken);
    }

    #endregion

    #region Column Filtering

    /// <summary>
    /// Translates MudDataGrid column filter definitions into EF Core WHERE clauses.
    /// Each filter definition specifies a column, operator, and value which are combined with AND logic.
    /// </summary>
    /// <param name="source">The current queryable to filter.</param>
    /// <param name="filters">Column filter definitions from <see cref="GridState{T}.FilterDefinitions"/>.</param>
    /// <returns>The filtered queryable with WHERE clauses appended.</returns>
    private IQueryable<T> ApplyColumnFilters(IQueryable<T> source, ICollection<IFilterDefinition<T>>? filters)
    {
        if (filters is null || filters.Count == 0)
            return source;

        foreach (var filter in filters)
        {
            var propertyName = filter.Column?.PropertyName;
            var op = filter.Operator?.Trim() ?? string.Empty;
            var val = filter.Value;

            if (string.IsNullOrWhiteSpace(propertyName))
                continue;

            // Try each selector type in order; skip if property not mapped
            if (_stringSelectors.TryGetValue(propertyName, out var strExpr))
            {
                source = ApplyStringFilter(source, strExpr, op, Convert.ToString(val) ?? string.Empty);
                continue;
            }

            if (_dateTimeSelectors.TryGetValue(propertyName, out var dtExpr))
            {
                source = ApplyDateTimeFilter(source, dtExpr, op, ParseDateTime(val));
                continue;
            }

            if (_boolSelectors.TryGetValue(propertyName, out var boolExpr))
            {
                source = ApplyBoolFilter(source, boolExpr, op, ParseBool(val));
                continue;
            }

            if (_intSelectors.TryGetValue(propertyName, out var intExpr))
            {
                source = ApplyIntFilter(source, intExpr, op, ParseInt(val));
                continue;
            }

            if (_longSelectors.TryGetValue(propertyName, out var longExpr))
            {
                source = ApplyNumericFilter(source, longExpr, op, ParseLong(val));
                continue;
            }

            if (_decimalSelectors.TryGetValue(propertyName, out var decExpr))
            {
                source = ApplyNumericFilter(source, decExpr, op, ParseDecimal(val));
                continue;
            }

            if (_doubleSelectors.TryGetValue(propertyName, out var dblExpr))
            {
                source = ApplyNumericFilter(source, dblExpr, op, ParseDouble(val));
                continue;
            }

            // Unmapped property — skip silently (defensive: grid may have columns without mapped filters)
        }

        return source;
    }

    /// <summary>
    /// Builds a WHERE clause for a string column using the specified operator.
    /// All string comparisons are case-insensitive via <c>ToLower()</c> which EF Core translates to SQL LOWER().
    /// </summary>
    private static IQueryable<T> ApplyStringFilter(IQueryable<T> source, Expression<Func<T, string?>> selector, string op, string val)
    {
        // Parameter reuse: all generated expressions share the same parameter from the selector
        var param = selector.Parameters[0];
        var memberAccess = selector.Body;

        switch (op)
        {
            case FilterOperator.String.Contains:
                {
                    // WHERE LOWER(property) LIKE '%value%' — case-insensitive contains
                    var lowerMember = Expression.Call(
                        Expression.Coalesce(memberAccess, Expression.Constant(string.Empty)),
                        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
                    var containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
                    var body = Expression.Call(lowerMember, containsMethod, Expression.Constant(val.ToLower()));
                    return source.Where(Expression.Lambda<Func<T, bool>>(body, param));
                }

            case FilterOperator.String.NotContains:
                {
                    // WHERE LOWER(property) NOT LIKE '%value%'
                    var lowerMember = Expression.Call(
                        Expression.Coalesce(memberAccess, Expression.Constant(string.Empty)),
                        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
                    var containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
                    var containsCall = Expression.Call(lowerMember, containsMethod, Expression.Constant(val.ToLower()));
                    var body = Expression.Not(containsCall);
                    return source.Where(Expression.Lambda<Func<T, bool>>(body, param));
                }

            case FilterOperator.String.Equal:
                {
                    // WHERE LOWER(property) = 'value'
                    var lowerMember = Expression.Call(
                        Expression.Coalesce(memberAccess, Expression.Constant(string.Empty)),
                        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
                    var body = Expression.Equal(lowerMember, Expression.Constant(val.ToLower()));
                    return source.Where(Expression.Lambda<Func<T, bool>>(body, param));
                }

            case FilterOperator.String.NotEqual:
                {
                    // WHERE LOWER(property) != 'value'
                    var lowerMember = Expression.Call(
                        Expression.Coalesce(memberAccess, Expression.Constant(string.Empty)),
                        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
                    var body = Expression.NotEqual(lowerMember, Expression.Constant(val.ToLower()));
                    return source.Where(Expression.Lambda<Func<T, bool>>(body, param));
                }

            case FilterOperator.String.StartsWith:
                {
                    // WHERE LOWER(property) LIKE 'value%'
                    var lowerMember = Expression.Call(
                        Expression.Coalesce(memberAccess, Expression.Constant(string.Empty)),
                        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
                    var startsWithMethod = typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!;
                    var body = Expression.Call(lowerMember, startsWithMethod, Expression.Constant(val.ToLower()));
                    return source.Where(Expression.Lambda<Func<T, bool>>(body, param));
                }

            case FilterOperator.String.EndsWith:
                {
                    // WHERE LOWER(property) LIKE '%value'
                    var lowerMember = Expression.Call(
                        Expression.Coalesce(memberAccess, Expression.Constant(string.Empty)),
                        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
                    var endsWithMethod = typeof(string).GetMethod(nameof(string.EndsWith), new[] { typeof(string) })!;
                    var body = Expression.Call(lowerMember, endsWithMethod, Expression.Constant(val.ToLower()));
                    return source.Where(Expression.Lambda<Func<T, bool>>(body, param));
                }

            case FilterOperator.String.Empty:
                {
                    // WHERE property IS NULL OR property = ''
                    var isNullOrEmpty = Expression.Call(
                        typeof(string).GetMethod(nameof(string.IsNullOrEmpty), new[] { typeof(string) })!,
                        memberAccess);
                    return source.Where(Expression.Lambda<Func<T, bool>>(isNullOrEmpty, param));
                }

            case FilterOperator.String.NotEmpty:
                {
                    // WHERE property IS NOT NULL AND property != ''
                    var isNullOrEmpty = Expression.Call(
                        typeof(string).GetMethod(nameof(string.IsNullOrEmpty), new[] { typeof(string) })!,
                        memberAccess);
                    var body = Expression.Not(isNullOrEmpty);
                    return source.Where(Expression.Lambda<Func<T, bool>>(body, param));
                }

            default:
                // Unknown operator — return unchanged (defensive)
                return source;
        }
    }

    /// <summary>
    /// Builds a WHERE clause for a DateTime column using the specified operator.
    /// Supports: Is, IsNot, After, OnOrAfter, Before, OnOrBefore, Empty, NotEmpty.
    /// </summary>
    private static IQueryable<T> ApplyDateTimeFilter(IQueryable<T> source, Expression<Func<T, DateTime?>> selector, string op, DateTime? val)
    {
        var param = selector.Parameters[0];
        var memberAccess = selector.Body;

        if (op == FilterOperator.DateTime.Empty)
        {
            var body = Expression.Equal(memberAccess, Expression.Constant(null, typeof(DateTime?)));
            return source.Where(Expression.Lambda<Func<T, bool>>(body, param));
        }

        if (op == FilterOperator.DateTime.NotEmpty)
        {
            var body = Expression.NotEqual(memberAccess, Expression.Constant(null, typeof(DateTime?)));
            return source.Where(Expression.Lambda<Func<T, bool>>(body, param));
        }

        if (val is null)
            return source;

        var valConstant = Expression.Constant((DateTime?)val.Value, typeof(DateTime?));

        Expression filterBody = op switch
        {
            FilterOperator.DateTime.Is => Expression.Equal(memberAccess, valConstant),
            FilterOperator.DateTime.IsNot => Expression.NotEqual(memberAccess, valConstant),
            FilterOperator.DateTime.After => Expression.GreaterThan(memberAccess, valConstant),
            FilterOperator.DateTime.OnOrAfter => Expression.GreaterThanOrEqual(memberAccess, valConstant),
            FilterOperator.DateTime.Before => Expression.LessThan(memberAccess, valConstant),
            FilterOperator.DateTime.OnOrBefore => Expression.LessThanOrEqual(memberAccess, valConstant),
            _ => null!
        };

        if (filterBody is null)
            return source;

        return source.Where(Expression.Lambda<Func<T, bool>>(filterBody, param));
    }

    /// <summary>
    /// Builds a WHERE clause for a bool column using the specified operator.
    /// </summary>
    private static IQueryable<T> ApplyBoolFilter(IQueryable<T> source, Expression<Func<T, bool?>> selector, string op, bool? val)
    {
        if (op != FilterOperator.Boolean.Is || val is null)
            return source;

        var param = selector.Parameters[0];
        var memberAccess = selector.Body;
        var valConstant = Expression.Constant((bool?)val.Value, typeof(bool?));
        var body = Expression.Equal(memberAccess, valConstant);
        return source.Where(Expression.Lambda<Func<T, bool>>(body, param));
    }

    /// <summary>
    /// Builds a WHERE clause for an int column using the specified numeric operator.
    /// Supports: Equal, NotEqual, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual, Empty, NotEmpty.
    /// </summary>
    private static IQueryable<T> ApplyIntFilter(IQueryable<T> source, Expression<Func<T, int?>> selector, string op, int? val)
    {
        var param = selector.Parameters[0];
        var memberAccess = selector.Body;

        if (op == FilterOperator.Number.Empty)
        {
            var body = Expression.Equal(memberAccess, Expression.Constant(null, typeof(int?)));
            return source.Where(Expression.Lambda<Func<T, bool>>(body, param));
        }

        if (op == FilterOperator.Number.NotEmpty)
        {
            var body = Expression.NotEqual(memberAccess, Expression.Constant(null, typeof(int?)));
            return source.Where(Expression.Lambda<Func<T, bool>>(body, param));
        }

        if (val is null)
            return source;

        var valConstant = Expression.Constant((int?)val.Value, typeof(int?));

        Expression filterBody = op switch
        {
            FilterOperator.Number.Equal => Expression.Equal(memberAccess, valConstant),
            FilterOperator.Number.NotEqual => Expression.NotEqual(memberAccess, valConstant),
            FilterOperator.Number.GreaterThan => Expression.GreaterThan(memberAccess, valConstant),
            FilterOperator.Number.GreaterThanOrEqual => Expression.GreaterThanOrEqual(memberAccess, valConstant),
            FilterOperator.Number.LessThan => Expression.LessThan(memberAccess, valConstant),
            FilterOperator.Number.LessThanOrEqual => Expression.LessThanOrEqual(memberAccess, valConstant),
            _ => null!
        };

        if (filterBody is null)
            return source;

        return source.Where(Expression.Lambda<Func<T, bool>>(filterBody, param));
    }

    /// <summary>
    /// Generic numeric filter for long, decimal, and double columns.
    /// Builds a WHERE clause using the specified numeric operator.
    /// </summary>
    private static IQueryable<T> ApplyNumericFilter<TNum>(IQueryable<T> source, Expression<Func<T, TNum?>> selector, string op, TNum? val)
        where TNum : struct
    {
        var param = selector.Parameters[0];
        var memberAccess = selector.Body;

        if (op == FilterOperator.Number.Empty)
        {
            var body = Expression.Equal(memberAccess, Expression.Constant(null, typeof(TNum?)));
            return source.Where(Expression.Lambda<Func<T, bool>>(body, param));
        }

        if (op == FilterOperator.Number.NotEmpty)
        {
            var body = Expression.NotEqual(memberAccess, Expression.Constant(null, typeof(TNum?)));
            return source.Where(Expression.Lambda<Func<T, bool>>(body, param));
        }

        if (val is null)
            return source;

        var valConstant = Expression.Constant((TNum?)val.Value, typeof(TNum?));

        Expression filterBody = op switch
        {
            FilterOperator.Number.Equal => Expression.Equal(memberAccess, valConstant),
            FilterOperator.Number.NotEqual => Expression.NotEqual(memberAccess, valConstant),
            FilterOperator.Number.GreaterThan => Expression.GreaterThan(memberAccess, valConstant),
            FilterOperator.Number.GreaterThanOrEqual => Expression.GreaterThanOrEqual(memberAccess, valConstant),
            FilterOperator.Number.LessThan => Expression.LessThan(memberAccess, valConstant),
            FilterOperator.Number.LessThanOrEqual => Expression.LessThanOrEqual(memberAccess, valConstant),
            _ => null!
        };

        if (filterBody is null)
            return source;

        return source.Where(Expression.Lambda<Func<T, bool>>(filterBody, param));
    }

    #endregion

    #region Global Search

    /// <summary>
    /// Applies a global search term as an OR condition across the specified string fields.
    /// Each field is checked for case-insensitive containment of the search term.
    /// This translates to: WHERE (LOWER(field1) LIKE '%term%') OR (LOWER(field2) LIKE '%term%') OR ...
    /// </summary>
    /// <param name="source">The current queryable.</param>
    /// <param name="searchTerm">The search text to match against. If null or whitespace, no filter is applied.</param>
    /// <param name="fieldNames">The registered string property names to search across.
    /// Only properties previously registered via <see cref="MapString"/> are included.</param>
    /// <returns>The filtered queryable with the global search OR clause appended.</returns>
    private IQueryable<T> ApplyGlobalSearch(IQueryable<T> source, string? searchTerm, IEnumerable<string>? fieldNames)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || fieldNames is null)
            return source;

        var fields = fieldNames.ToList();
        if (fields.Count == 0)
            return source;

        var lowerTerm = searchTerm!.ToLower();

        // Build a single expression parameter shared across all OR branches
        var param = Expression.Parameter(typeof(T), "x");
        Expression? combinedOr = null;

        foreach (var fieldName in fields)
        {
            if (!_stringSelectors.TryGetValue(fieldName, out var selectorExpr))
                continue;

            // Rebind the selector expression to use our shared parameter
            var reboundBody = new ParameterReplacer(selectorExpr.Parameters[0], param).Visit(selectorExpr.Body);

            // Build: (field ?? "").ToLower().Contains(lowerTerm)
            var coalesced = Expression.Coalesce(reboundBody, Expression.Constant(string.Empty));
            var toLower = Expression.Call(coalesced, typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
            var containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
            var containsCall = Expression.Call(toLower, containsMethod, Expression.Constant(lowerTerm));

            // Combine with OR: (prevCondition) || (currentContains)
            combinedOr = combinedOr is null
                ? (Expression)containsCall
                : Expression.OrElse(combinedOr, containsCall);
        }

        if (combinedOr is null)
            return source;

        var lambda = Expression.Lambda<Func<T, bool>>(combinedOr, param);
        return source.Where(lambda);
    }

    #endregion

    #region Sorting

    /// <summary>
    /// Applies sort definitions from GridState to the queryable.
    /// If no sort definitions are present, uses the explicit <see cref="DefaultSort"/> configuration,
    /// or falls back to the first registered DateTime field descending.
    /// Multi-sort is applied in index order: the first sort becomes OrderBy, subsequent sorts become ThenBy.
    /// </summary>
    /// <param name="source">The current queryable to sort.</param>
    /// <param name="sortDefinitions">Sort definitions from <see cref="GridState{T}.SortDefinitions"/>.</param>
    /// <returns>The ordered queryable.</returns>
    private IQueryable<T> ApplySorting(IQueryable<T> source, ICollection<SortDefinition<T>>? sortDefinitions)
    {
        // If no sort definitions, apply default sort
        if (sortDefinitions is null || sortDefinitions.Count == 0)
        {
            return ApplyDefaultSort(source);
        }

        IOrderedQueryable<T>? ordered = null;

        foreach (var sort in sortDefinitions.OrderBy(s => s.Index))
        {
            var prop = sort.SortBy;
            if (string.IsNullOrWhiteSpace(prop))
                continue;

            // Try each selector type to find the matching property for ORDER BY
            if (_stringSelectors.TryGetValue(prop, out var strExpr))
            {
                ordered = ApplyOrderBy(ordered ?? (IOrderedQueryable<T>?)null, source, strExpr, sort.Descending, ordered is not null);
                continue;
            }

            if (_dateTimeSelectors.TryGetValue(prop, out var dtExpr))
            {
                ordered = ApplyOrderBy(ordered ?? (IOrderedQueryable<T>?)null, source, dtExpr, sort.Descending, ordered is not null);
                continue;
            }

            if (_intSelectors.TryGetValue(prop, out var intExpr))
            {
                ordered = ApplyOrderBy(ordered ?? (IOrderedQueryable<T>?)null, source, intExpr, sort.Descending, ordered is not null);
                continue;
            }

            if (_longSelectors.TryGetValue(prop, out var longExpr))
            {
                ordered = ApplyOrderBy(ordered ?? (IOrderedQueryable<T>?)null, source, longExpr, sort.Descending, ordered is not null);
                continue;
            }

            if (_decimalSelectors.TryGetValue(prop, out var decExpr))
            {
                ordered = ApplyOrderBy(ordered ?? (IOrderedQueryable<T>?)null, source, decExpr, sort.Descending, ordered is not null);
                continue;
            }

            if (_doubleSelectors.TryGetValue(prop, out var dblExpr))
            {
                ordered = ApplyOrderBy(ordered ?? (IOrderedQueryable<T>?)null, source, dblExpr, sort.Descending, ordered is not null);
                continue;
            }

            if (_boolSelectors.TryGetValue(prop, out var boolExpr))
            {
                ordered = ApplyOrderBy(ordered ?? (IOrderedQueryable<T>?)null, source, boolExpr, sort.Descending, ordered is not null);
                continue;
            }

            // Unmapped sort property — skip silently
        }

        return ordered ?? source;
    }

    /// <summary>
    /// Applies the configured default sort. Priority:
    /// 1. Explicit <see cref="DefaultSort"/> configuration
    /// 2. First registered DateTime field (descending)
    /// 3. No sort (returns source as-is)
    /// </summary>
    private IQueryable<T> ApplyDefaultSort(IQueryable<T> source)
    {
        // Priority 1: Explicit default sort via DefaultSort() method
        if (_defaultSortProperty is not null)
        {
            if (_stringSelectors.TryGetValue(_defaultSortProperty, out var strExpr))
                return _defaultSortDescending ? source.OrderByDescending(strExpr) : source.OrderBy(strExpr);
            if (_dateTimeSelectors.TryGetValue(_defaultSortProperty, out var dtExpr))
                return _defaultSortDescending ? source.OrderByDescending(dtExpr) : source.OrderBy(dtExpr);
            if (_intSelectors.TryGetValue(_defaultSortProperty, out var intExpr))
                return _defaultSortDescending ? source.OrderByDescending(intExpr) : source.OrderBy(intExpr);
            if (_longSelectors.TryGetValue(_defaultSortProperty, out var longExpr))
                return _defaultSortDescending ? source.OrderByDescending(longExpr) : source.OrderBy(longExpr);
            if (_decimalSelectors.TryGetValue(_defaultSortProperty, out var decExpr))
                return _defaultSortDescending ? source.OrderByDescending(decExpr) : source.OrderBy(decExpr);
            if (_doubleSelectors.TryGetValue(_defaultSortProperty, out var dblExpr))
                return _defaultSortDescending ? source.OrderByDescending(dblExpr) : source.OrderBy(dblExpr);
            if (_boolSelectors.TryGetValue(_defaultSortProperty, out var boolExpr))
                return _defaultSortDescending ? source.OrderByDescending(boolExpr) : source.OrderBy(boolExpr);
        }

        // Priority 2: First registered DateTime field descending
        if (_firstDateTimeProperty is not null && _dateTimeSelectors.TryGetValue(_firstDateTimeProperty, out var defaultDtExpr))
        {
            return source.OrderByDescending(defaultDtExpr);
        }

        // Priority 3: No sort possible
        return source;
    }

    /// <summary>
    /// Applies OrderBy/ThenBy (or their descending variants) to a queryable using the given expression.
    /// </summary>
    /// <typeparam name="TKey">The type of the sort key.</typeparam>
    /// <param name="currentOrdered">The currently ordered queryable (if multi-sort), or null for the first sort.</param>
    /// <param name="source">The original unordered queryable (used for the first OrderBy call).</param>
    /// <param name="keySelector">The expression selecting the sort key.</param>
    /// <param name="descending">Whether to sort descending.</param>
    /// <param name="isThenBy">Whether this is a subsequent sort (ThenBy) rather than the first (OrderBy).</param>
    /// <returns>The ordered queryable.</returns>
    private static IOrderedQueryable<T> ApplyOrderBy<TKey>(
        IOrderedQueryable<T>? currentOrdered,
        IQueryable<T> source,
        Expression<Func<T, TKey>> keySelector,
        bool descending,
        bool isThenBy)
    {
        if (isThenBy && currentOrdered is not null)
        {
            return descending
                ? currentOrdered.ThenByDescending(keySelector)
                : currentOrdered.ThenBy(keySelector);
        }

        return descending
            ? (currentOrdered ?? source).OrderByDescending(keySelector)
            : (currentOrdered ?? source).OrderBy(keySelector);
    }

    #endregion

    #region Expression Helpers

    /// <summary>
    /// Replaces all occurrences of one parameter expression with another in an expression tree.
    /// Used to rebind selector expressions to a shared parameter when building composite OR expressions for global search.
    /// </summary>
    private sealed class ParameterReplacer : System.Linq.Expressions.ExpressionVisitor
    {
        private readonly ParameterExpression _oldParam;
        private readonly ParameterExpression _newParam;

        /// <summary>
        /// Initializes a new instance of the <see cref="ParameterReplacer"/> class.
        /// </summary>
        /// <param name="oldParam">The original parameter to replace.</param>
        /// <param name="newParam">The new parameter to substitute.</param>
        public ParameterReplacer(ParameterExpression oldParam, ParameterExpression newParam)
        {
            _oldParam = oldParam;
            _newParam = newParam;
        }

        /// <inheritdoc />
        protected override Expression VisitParameter(ParameterExpression node)
            => node == _oldParam ? _newParam : base.VisitParameter(node);
    }

    #endregion

    #region Value Parsers

    /// <summary>
    /// Attempts to parse an object as a <see cref="DateTime"/>.
    /// </summary>
    private static DateTime? ParseDateTime(object? value)
        => value is DateTime dt ? dt : DateTime.TryParse(Convert.ToString(value), out var parsed) ? parsed : null;

    /// <summary>
    /// Attempts to parse an object as a nullable <see cref="bool"/>.
    /// </summary>
    private static bool? ParseBool(object? value)
    {
        if (value is bool b) return b;
        var s = Convert.ToString(value)?.Trim();
        if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
        return null;
    }

    /// <summary>
    /// Attempts to parse an object as a nullable <see cref="int"/>.
    /// </summary>
    private static int? ParseInt(object? value)
        => value is int i ? i : int.TryParse(Convert.ToString(value), out var parsed) ? parsed : null;

    /// <summary>
    /// Attempts to parse an object as a nullable <see cref="long"/>.
    /// </summary>
    private static long? ParseLong(object? value)
        => value is long l ? l : long.TryParse(Convert.ToString(value), out var parsed) ? parsed : null;

    /// <summary>
    /// Attempts to parse an object as a nullable <see cref="decimal"/>.
    /// </summary>
    private static decimal? ParseDecimal(object? value)
        => value is decimal d ? d : decimal.TryParse(Convert.ToString(value), out var parsed) ? parsed : null;

    /// <summary>
    /// Attempts to parse an object as a nullable <see cref="double"/>.
    /// </summary>
    private static double? ParseDouble(object? value)
        => value is double d ? d : double.TryParse(Convert.ToString(value), out var parsed) ? parsed : null;

    #endregion
}
