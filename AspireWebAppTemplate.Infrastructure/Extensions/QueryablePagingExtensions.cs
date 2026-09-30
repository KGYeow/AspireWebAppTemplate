using System.Linq.Expressions;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Extensions;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.Infrastructure.Extensions;

/// <summary>
/// Entity Framework Core extension methods that turn a composed <see cref="IQueryable{T}"/> into a
/// <see cref="PagedResult{TDto}"/> at the database level. Sorting, counting, paging, and projection
/// are all translated to SQL and executed by the database, so only a single page of rows is
/// materialized.
/// </summary>
/// <remarks>
/// This helper lives in the Infrastructure layer because it depends on EF Core async operators
/// (<see cref="EntityFrameworkQueryableExtensions.CountAsync{TSource}(IQueryable{TSource}, CancellationToken)"/>
/// and <see cref="EntityFrameworkQueryableExtensions.ToListAsync{TSource}(IQueryable{TSource}, CancellationToken)"/>).
/// The Application layer intentionally has no EF Core reference, which is why the provider-agnostic
/// <see cref="QueryableExtensions.ApplySort{T}"/> sort helper stays in Application while this
/// database-aware paging helper stays here. Feature services apply their own <c>Where</c> filters to
/// the query before calling this method; the filtering step remains explicit per feature so it stays
/// readable and reliably translatable to SQL.
/// </remarks>
public static class QueryablePagingExtensions
{
    /// <summary>
    /// Applies dynamic sorting, counts the total matching rows, pages the query, and projects each row
    /// to <typeparamref name="TDto"/> — all executed by the database — returning a populated
    /// <see cref="PagedResult{TDto}"/>.
    /// </summary>
    /// <typeparam name="T">The entity type being queried.</typeparam>
    /// <typeparam name="TDto">The projected result type returned to callers.</typeparam>
    /// <param name="query">The already-filtered source query (apply <c>Where</c> clauses before calling).</param>
    /// <param name="page">The zero-based page index to retrieve.</param>
    /// <param name="pageSize">The maximum number of items per page.</param>
    /// <param name="sortBy">The property name to sort by (case-insensitive). Null/empty uses <paramref name="defaultSort"/>.</param>
    /// <param name="sortDescending">Whether to sort in descending order when <paramref name="sortBy"/> is applied.</param>
    /// <param name="defaultSort">The fallback ordering applied when <paramref name="sortBy"/> is null, empty, or unknown.</param>
    /// <param name="projection">The row-to-DTO projection, kept as an expression so EF Core projects it in SQL.</param>
    /// <param name="cancellationToken">A token to observe while awaiting the database calls.</param>
    /// <returns>A <see cref="PagedResult{TDto}"/> containing the page items, total count, page index, and page size.</returns>
    public static async Task<PagedResult<TDto>> ToPagedResultAsync<T, TDto>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        string? sortBy,
        bool sortDescending,
        Func<IQueryable<T>, IOrderedQueryable<T>> defaultSort,
        Expression<Func<T, TDto>> projection,
        CancellationToken cancellationToken = default)
    {
        // Count the full result set before paging so the total reflects all matching rows.
        var totalCount = await query.CountAsync(cancellationToken);

        // Sort, page, and project — all translated to SQL — materializing only the requested page.
        var items = await query
            .ApplySort(sortBy, sortDescending, defaultSort)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<TDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}