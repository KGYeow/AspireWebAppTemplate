using System.Linq.Expressions;

namespace AspireWebAppTemplate.Application.Extensions;

/// <summary>
/// Extension methods for <see cref="IQueryable{T}"/> providing dynamic sorting
/// capabilities for server-side data grid operations. Designed to work with
/// Entity Framework Core and translate to SQL ORDER BY clauses.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Applies dynamic sorting to the queryable based on a property name string.
    /// Falls back to the specified default ordering if the property name is null, empty,
    /// or does not match a property on the entity type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="sortBy">The property name to sort by (case-insensitive). Null/empty uses the default.</param>
    /// <param name="descending">Whether to sort in descending order.</param>
    /// <param name="defaultSort">A fallback ordering function applied when sortBy is invalid or empty.</param>
    /// <returns>The ordered queryable.</returns>
    public static IOrderedQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        string? sortBy,
        bool descending,
        Func<IQueryable<T>, IOrderedQueryable<T>> defaultSort)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
            return defaultSort(query);

        // Find the property by name (case-insensitive)
        var property = typeof(T).GetProperty(sortBy,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

        if (property is null)
            return defaultSort(query);

        // Build expression: x => x.PropertyName
        var parameter = Expression.Parameter(typeof(T), "x");
        var propertyAccess = Expression.Property(parameter, property);
        var lambda = Expression.Lambda(propertyAccess, parameter);

        // Call OrderBy or OrderByDescending dynamically
        var methodName = descending ? "OrderByDescending" : "OrderBy";
        var resultExpression = Expression.Call(
            typeof(Queryable),
            methodName,
            new[] { typeof(T), property.PropertyType },
            query.Expression,
            Expression.Quote(lambda));

        return (IOrderedQueryable<T>)query.Provider.CreateQuery<T>(resultExpression);
    }
}
