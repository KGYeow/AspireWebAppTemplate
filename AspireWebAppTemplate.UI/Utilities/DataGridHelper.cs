using System;
using System.Collections.Generic;
using System.Linq;
using MudBlazor;

namespace AspireWebAppTemplate.UI.Utilities;

/// <summary>
/// Reusable server-side helper for MudBlazor <see cref="MudDataGrid{T}"/>.
/// Applies column filters, multi-sort, global search, and pagination
/// based on <see cref="GridState{T}"/>.
/// </summary>
/// <typeparam name="T">The data item type displayed in the grid.</typeparam>
/// <remarks>
/// <para>Usage: create an instance, register property selectors via the fluent
/// <c>Map*</c> methods, then pass <see cref="ServerReloadAsync"/> as the
/// <c>ServerData</c> callback on <see cref="MudDataGrid{T}"/>.</para>
/// <para>Example:</para>
/// <code>
/// private readonly DataGridHelper&lt;MyItem&gt; _gridUtils = new DataGridHelper&lt;MyItem&gt;()
///     .MapString(nameof(MyItem.Name), x =&gt; x.Name)
///     .MapInt(nameof(MyItem.Id), x =&gt; x.Id)
///     .MapDateTime(nameof(MyItem.CreatedAt), x =&gt; x.CreatedAt);
///
/// private Task&lt;GridData&lt;MyItem&gt;&gt; ServerReload(GridState&lt;MyItem&gt; state)
///     =&gt; _gridUtils.ServerReloadAsync(state, () =&gt; myService.GetAllAsync());
/// </code>
/// </remarks>
public sealed class DataGridHelper<T>
{
    #region Selector Registries

    private readonly Dictionary<string, Func<T, string?>> _stringSelectors = new();
    private readonly Dictionary<string, Func<T, int?>> _intSelectors = new();
    private readonly Dictionary<string, Func<T, decimal?>> _decimalSelectors = new();
    private readonly Dictionary<string, Func<T, double?>> _doubleSelectors = new();
    private readonly Dictionary<string, Func<T, long?>> _longSelectors = new();
    private readonly Dictionary<string, Func<T, DateTime?>> _dateSelectors = new();
    private readonly Dictionary<string, Func<T, bool?>> _boolSelectors = new();
    private readonly Dictionary<string, Func<T, string>> _enumSelectors = new();

    #endregion

    #region Mapping API

    /// <summary>
    /// Registers a string property selector for filtering and sorting.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c> (typically <c>nameof(T.Property)</c>).</param>
    /// <param name="selector">A function that extracts the string value from an item.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public DataGridHelper<T> MapString(string propertyName, Func<T, string?> selector)
    { _stringSelectors[propertyName] = selector; return this; }

    /// <summary>
    /// Registers a non-nullable int property selector.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c>.</param>
    /// <param name="selector">A function that extracts the int value from an item.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public DataGridHelper<T> MapInt(string propertyName, Func<T, int> selector)
    { _intSelectors[propertyName] = x => selector(x); return this; }

    /// <summary>
    /// Registers a nullable int property selector.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c>.</param>
    /// <param name="selector">A function that extracts the nullable int value from an item.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public DataGridHelper<T> MapNullableInt(string propertyName, Func<T, int?> selector)
    { _intSelectors[propertyName] = selector; return this; }

    /// <summary>
    /// Registers a nullable decimal property selector.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c>.</param>
    /// <param name="selector">A function that extracts the nullable decimal value from an item.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public DataGridHelper<T> MapDecimal(string propertyName, Func<T, decimal?> selector)
    { _decimalSelectors[propertyName] = selector; return this; }

    /// <summary>
    /// Registers a nullable double property selector.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c>.</param>
    /// <param name="selector">A function that extracts the nullable double value from an item.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public DataGridHelper<T> MapDouble(string propertyName, Func<T, double?> selector)
    { _doubleSelectors[propertyName] = selector; return this; }

    /// <summary>
    /// Registers a nullable long property selector.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c>.</param>
    /// <param name="selector">A function that extracts the nullable long value from an item.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public DataGridHelper<T> MapLong(string propertyName, Func<T, long?> selector)
    { _longSelectors[propertyName] = selector; return this; }

    /// <summary>
    /// Registers a nullable DateTime property selector.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c>.</param>
    /// <param name="selector">A function that extracts the nullable DateTime value from an item.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public DataGridHelper<T> MapDateTime(string propertyName, Func<T, DateTime?> selector)
    { _dateSelectors[propertyName] = selector; return this; }

    /// <summary>
    /// Registers a nullable bool property selector.
    /// </summary>
    /// <param name="propertyName">The column's <c>PropertyName</c>.</param>
    /// <param name="selector">A function that extracts the nullable bool value from an item.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public DataGridHelper<T> MapBool(string propertyName, Func<T, bool?> selector)
    { _boolSelectors[propertyName] = selector; return this; }

    /// <summary>
    /// Registers an enum property selector for filtering and sorting.
    /// Stores the enum's <c>ToString()</c> representation for comparison against filter values
    /// set by <c>EnumFilterSelect</c> (which uses <c>FilterOperator.Enum.Is</c>).
    /// </summary>
    /// <typeparam name="TEnum">The enum type of the property.</typeparam>
    /// <param name="propertyName">The column's <c>PropertyName</c> (typically <c>nameof(T.Property)</c>).</param>
    /// <param name="selector">A function that extracts the enum value from an item.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public DataGridHelper<T> MapEnum<TEnum>(string propertyName, Func<T, TEnum> selector) where TEnum : struct, Enum
    { _enumSelectors[propertyName] = x => selector(x).ToString(); return this; }

    #endregion

    #region Public Entry Point

    /// <summary>
    /// Full server-side pipeline: load → filter → global search → sort → paginate → optional line numbering → <see cref="GridData{T}"/>.
    /// </summary>
    /// <param name="state">The current grid state containing page, page size, filters, and sort definitions.</param>
    /// <param name="loadItemsAsync">An async function that loads the full data set (before filtering/paging).</param>
    /// <param name="globalSearchTerm">Optional global search term applied across all fields returned by <paramref name="globalSearchFieldSelector"/>.</param>
    /// <param name="globalSearchFieldSelector">
    /// Optional function that returns the searchable string values for a given item.
    /// Each returned string is checked against <paramref name="globalSearchTerm"/> using case-insensitive contains.
    /// </param>
    /// <param name="setLineNumber">
    /// Optional callback to set a display line number on each item in the current page.
    /// Receives the item and its 1-based line number (accounting for page offset).
    /// </param>
    /// <returns>A <see cref="GridData{T}"/> containing the paged items and total count.</returns>
    public async Task<GridData<T>> ServerReloadAsync(
        GridState<T> state,
        Func<Task<IEnumerable<T>>> loadItemsAsync,
        string? globalSearchTerm = null,
        Func<T, IEnumerable<string>>? globalSearchFieldSelector = null,
        Action<T, int>? setLineNumber = null)
    {
        // 1) Load items
        var items = await loadItemsAsync();
        var query = items.AsEnumerable();

        // 2) Apply column filters
        query = ApplyColumnFilters(query, state.FilterDefinitions);

        // 3) Apply optional global search
        if (!string.IsNullOrWhiteSpace(globalSearchTerm) && globalSearchFieldSelector is not null)
        {
            var term = globalSearchTerm!;
            query = query.Where(x =>
                globalSearchFieldSelector(x).Any(field =>
                    field is not null && field.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        // 4) Apply multi-sort
        query = ApplySorts(query, state.SortDefinitions);

        // 5) Count total then paginate
        var total = query.Count();
        var pageItems = query
            .Skip(state.Page * state.PageSize)
            .Take(state.PageSize)
            .ToList();

        // 6) Optional line numbering
        if (setLineNumber is not null)
        {
            for (var i = 0; i < pageItems.Count; i++)
            {
                setLineNumber(pageItems[i], state.Page * state.PageSize + i + 1);
            }
        }

        // 7) Return
        return new GridData<T> { Items = pageItems, TotalItems = total };
    }

    #endregion

    #region Filtering

    /// <summary>
    /// Applies all column filter definitions to the source sequence.
    /// </summary>
    private IEnumerable<T> ApplyColumnFilters(IEnumerable<T> source, ICollection<IFilterDefinition<T>>? filters)
    {
        if (filters is null || filters.Count == 0)
            return source;

        var result = source;

        foreach (var f in filters)
        {
            var propertyName = f.Column?.PropertyName;
            var op = f.Operator?.Trim() ?? string.Empty;
            var val = f.Value;

            if (string.IsNullOrWhiteSpace(propertyName))
                continue;

            // String
            if (_stringSelectors.TryGetValue(propertyName, out var strSelector))
            {
                result = ApplyStringFilter(result, strSelector, op, Convert.ToString(val) ?? string.Empty);
                continue;
            }

            // Enum (mapped via MapEnum — compares enum ToString() against filter value)
            if (_enumSelectors.TryGetValue(propertyName, out var enumSelector))
            {
                var enumVal = val is Enum ? val.ToString() ?? string.Empty : Convert.ToString(val) ?? string.Empty;
                result = ApplyEnumFilter(result, enumSelector, op, enumVal);
                continue;
            }

            // DateTime
            if (_dateSelectors.TryGetValue(propertyName, out var dtSelector))
            {
                result = ApplyDateFilter(result, dtSelector, op, ToDate(val));
                continue;
            }

            // Boolean
            if (_boolSelectors.TryGetValue(propertyName, out var boolSelector))
            {
                result = ApplyBoolFilter(result, boolSelector, op, ToBoolNullable(val));
                continue;
            }

            // Int
            if (_intSelectors.TryGetValue(propertyName, out var intSelector))
            {
                result = ApplyNumberFilter(result, intSelector, op, ToNullableInt(val));
                continue;
            }

            // Long
            if (_longSelectors.TryGetValue(propertyName, out var longSelector))
            {
                result = ApplyNumberFilter(result, longSelector, op, ToNullableLong(val));
                continue;
            }

            // Double
            if (_doubleSelectors.TryGetValue(propertyName, out var dblSelector))
            {
                result = ApplyNumberFilter(result, dblSelector, op, ToNullableDouble(val));
                continue;
            }

            // Decimal
            if (_decimalSelectors.TryGetValue(propertyName, out var decSelector))
            {
                result = ApplyNumberFilter(result, decSelector, op, ToNullableDecimal(val));
                continue;
            }

            // Unmapped property — skip silently
        }

        return result;
    }

    /// <summary>
    /// Applies a string filter operator (contains, not contains, equals, starts with, etc.).
    /// </summary>
    private static IEnumerable<T> ApplyStringFilter(IEnumerable<T> source, Func<T, string?> sel, string op, string val)
    {
        Func<string?, bool> predicate = op switch
        {
            FilterOperator.String.Contains    => s => s?.IndexOf(val, StringComparison.OrdinalIgnoreCase) >= 0,
            FilterOperator.String.NotContains => s => s is null || s.IndexOf(val, StringComparison.OrdinalIgnoreCase) < 0,
            FilterOperator.String.Equal       => s => string.Equals(s ?? "", val, StringComparison.OrdinalIgnoreCase),
            FilterOperator.String.NotEqual    => s => !string.Equals(s ?? "", val, StringComparison.OrdinalIgnoreCase),
            FilterOperator.String.StartsWith  => s => s?.StartsWith(val, StringComparison.OrdinalIgnoreCase) == true,
            FilterOperator.String.EndsWith    => s => s?.EndsWith(val, StringComparison.OrdinalIgnoreCase) == true,
            FilterOperator.String.Empty       => s => string.IsNullOrEmpty(s),
            FilterOperator.String.NotEmpty    => s => !string.IsNullOrEmpty(s),
            _ => _ => true
        };
        return source.Where(x => predicate(sel(x)));
    }

    /// <summary>
    /// Applies an enum filter operator (is, is not). Compares the item's enum ToString()
    /// value against the filter value using case-insensitive string comparison.
    /// </summary>
    private static IEnumerable<T> ApplyEnumFilter(IEnumerable<T> source, Func<T, string> sel, string op, string val)
    {
        Func<string, bool> predicate = op switch
        {
            FilterOperator.Enum.Is    => s => string.Equals(s, val, StringComparison.OrdinalIgnoreCase),
            FilterOperator.Enum.IsNot => s => !string.Equals(s, val, StringComparison.OrdinalIgnoreCase),
            _ => _ => true
        };
        return source.Where(x => predicate(sel(x)));
    }

    /// <summary>
    /// Applies a DateTime filter operator (is, is not, after, before, etc.).
    /// </summary>
    private static IEnumerable<T> ApplyDateFilter(IEnumerable<T> source, Func<T, DateTime?> sel, string op, DateTime? dt)
    {
        if (op == FilterOperator.DateTime.Empty)    return source.Where(x => sel(x) == null);
        if (op == FilterOperator.DateTime.NotEmpty) return source.Where(x => sel(x) != null);
        if (dt == null) return source;

        return op switch
        {
            FilterOperator.DateTime.Is         => source.Where(x => sel(x) == dt),
            FilterOperator.DateTime.IsNot      => source.Where(x => sel(x) != dt),
            FilterOperator.DateTime.After      => source.Where(x => sel(x) > dt),
            FilterOperator.DateTime.OnOrAfter  => source.Where(x => sel(x) >= dt),
            FilterOperator.DateTime.Before     => source.Where(x => sel(x) < dt),
            FilterOperator.DateTime.OnOrBefore => source.Where(x => sel(x) <= dt),
            _ => source
        };
    }

    /// <summary>
    /// Applies a boolean filter operator.
    /// </summary>
    private static IEnumerable<T> ApplyBoolFilter(IEnumerable<T> source, Func<T, bool?> sel, string op, bool? val)
    {
        return op switch
        {
            FilterOperator.Boolean.Is => source.Where(x => sel(x) == val),
            _ => source
        };
    }

    /// <summary>
    /// Applies a numeric filter operator (=, !=, &gt;, &gt;=, &lt;, &lt;=, empty, not empty).
    /// Works with any <see cref="IComparable{T}"/> numeric type.
    /// </summary>
    private static IEnumerable<T> ApplyNumberFilter<TNum>(IEnumerable<T> source, Func<T, TNum?> sel, string op, TNum? val)
        where TNum : struct, IComparable<TNum>
    {
        if (op == FilterOperator.Number.Empty)    return source.Where(x => sel(x) == null);
        if (op == FilterOperator.Number.NotEmpty) return source.Where(x => sel(x) != null);
        if (val is null) return source;

        return op switch
        {
            FilterOperator.Number.Equal              => source.Where(x => NullableCompare(sel(x), val) == 0),
            FilterOperator.Number.NotEqual           => source.Where(x => NullableCompare(sel(x), val) != 0),
            FilterOperator.Number.GreaterThan        => source.Where(x => NullableCompare(sel(x), val) > 0),
            FilterOperator.Number.GreaterThanOrEqual => source.Where(x => NullableCompare(sel(x), val) >= 0),
            FilterOperator.Number.LessThan           => source.Where(x => NullableCompare(sel(x), val) < 0),
            FilterOperator.Number.LessThanOrEqual    => source.Where(x => NullableCompare(sel(x), val) <= 0),
            _ => source
        };
    }

    /// <summary>
    /// Compares two nullable values. Null is treated as the smallest possible value.
    /// </summary>
    private static int NullableCompare<TNum>(TNum? a, TNum? b) where TNum : struct, IComparable<TNum>
    {
        if (!a.HasValue && !b.HasValue) return 0;
        if (!a.HasValue) return -1;
        if (!b.HasValue) return 1;
        return a.Value.CompareTo(b.Value);
    }

    #endregion

    #region Sorting

    /// <summary>
    /// Applies multi-column sorting based on <see cref="SortDefinition{T}"/> index order.
    /// </summary>
    private IEnumerable<T> ApplySorts(IEnumerable<T> source, ICollection<SortDefinition<T>>? sorts)
    {
        if (sorts is null || sorts.Count == 0)
            return source;

        IOrderedEnumerable<T>? ordered = null;

        foreach (var s in sorts.OrderBy(d => d.Index))
        {
            var prop = s.SortBy;
            if (string.IsNullOrWhiteSpace(prop))
                continue;

            var keySelector = TryGetSortKeySelector(prop);
            if (keySelector is null)
                continue;

            if (ordered is null)
            {
                ordered = s.Descending
                    ? source.OrderByDescending(keySelector)
                    : source.OrderBy(keySelector);
            }
            else
            {
                ordered = s.Descending
                    ? ordered.ThenByDescending(keySelector)
                    : ordered.ThenBy(keySelector);
            }
        }

        return ordered ?? source;
    }

    /// <summary>
    /// Attempts to find a sort key selector for the given property name
    /// across all registered selector dictionaries.
    /// </summary>
    /// <param name="propertyName">The property name to look up.</param>
    /// <returns>A boxed key selector, or <c>null</c> if the property is not mapped.</returns>
    private Func<T, object?>? TryGetSortKeySelector(string propertyName)
    {
        if (_stringSelectors.TryGetValue(propertyName, out var s))   return x => s(x);
        if (_enumSelectors.TryGetValue(propertyName, out var e))     return x => e(x);
        if (_dateSelectors.TryGetValue(propertyName, out var d))     return x => d(x);
        if (_boolSelectors.TryGetValue(propertyName, out var b))     return x => b(x);
        if (_intSelectors.TryGetValue(propertyName, out var i))      return x => i(x);
        if (_longSelectors.TryGetValue(propertyName, out var l))     return x => l(x);
        if (_doubleSelectors.TryGetValue(propertyName, out var db))  return x => db(x);
        if (_decimalSelectors.TryGetValue(propertyName, out var dc)) return x => dc(x);
        return null;
    }

    #endregion

    #region Parser Helpers

    /// <summary>
    /// Attempts to parse an object as a <see cref="DateTime"/>.
    /// </summary>
    private static DateTime? ToDate(object? v)
        => v is DateTime d ? d : DateTime.TryParse(Convert.ToString(v), out var parsed) ? parsed : null;

    /// <summary>
    /// Attempts to parse an object as a nullable <see cref="bool"/>.
    /// </summary>
    private static bool? ToBoolNullable(object? v)
    {
        if (v is bool b) return b;
        var s = Convert.ToString(v)?.Trim();
        if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
        return null;
    }

    /// <summary>Attempts to parse an object as a nullable <see cref="int"/>.</summary>
    private static int? ToNullableInt(object? v)
        => v is int i ? i : int.TryParse(Convert.ToString(v), out var parsed) ? parsed : null;

    /// <summary>Attempts to parse an object as a nullable <see cref="long"/>.</summary>
    private static long? ToNullableLong(object? v)
        => v is long l ? l : long.TryParse(Convert.ToString(v), out var parsed) ? parsed : null;

    /// <summary>Attempts to parse an object as a nullable <see cref="double"/>.</summary>
    private static double? ToNullableDouble(object? v)
        => v is double d ? d : double.TryParse(Convert.ToString(v), out var parsed) ? parsed : null;

    /// <summary>Attempts to parse an object as a nullable <see cref="decimal"/>.</summary>
    private static decimal? ToNullableDecimal(object? v)
        => v is decimal d ? d : decimal.TryParse(Convert.ToString(v), out var parsed) ? parsed : null;

    #endregion
}
