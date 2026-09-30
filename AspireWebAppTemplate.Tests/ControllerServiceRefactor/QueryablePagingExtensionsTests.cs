// Feature: queryable-datagrid-helper: ToPagedResultAsync database-level paging invariants
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Extensions;
using AspireWebAppTemplate.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.Tests.ControllerServiceRefactor;

/// <summary>
/// Property-based tests verifying that the reusable <see cref="QueryablePagingExtensions.ToPagedResultAsync{T, TDto}"/>
/// helper produces a <see cref="PagedResult{TDto}"/> whose invariants hold for any valid page/pageSize:
/// the page never exceeds the requested size, the echoed Page/PageSize match the request, the total
/// count reflects the whole (filtered) set, and the projection is applied. Uses a real SQLite in-memory
/// database so the sort/count/skip/take/project pipeline is exercised through EF Core, not LINQ-to-Objects.
/// </summary>
public class QueryablePagingExtensionsTests
{
    #region Helpers

    /// <summary>
    /// Creates a fresh SQLite in-memory ApplicationDbContext with the schema created and foreign keys
    /// disabled, so AuditLogEntry rows can be seeded without a matching ApplicationUser.
    /// </summary>
    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        var context = new ApplicationDbContext(options);
        context.Database.OpenConnection();
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Seeds the context with <paramref name="count"/> audit log entries with descending timestamps.
    /// </summary>
    private static void Seed(ApplicationDbContext context, int count)
    {
        for (var i = 0; i < count; i++)
        {
            context.AuditLogEntries.Add(new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                UserId = $"user-{i}",
                UserDisplayName = $"User {i}",
                ActionType = AuditActionType.UserCreated,
                EntityType = AuditEntityType.User,
                EntityId = $"entity-{i}",
                EntityName = $"Entity {i}",
                Description = $"Action {i}",
                IpAddress = "10.0.0.1",
                Timestamp = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        context.SaveChanges();
    }

    #endregion

    /// <summary>
    /// Property: for any page (0-5) and pageSize (1-10) over a fixed seeded set, the paged result
    /// satisfies Items.Count &lt;= PageSize, Page/PageSize echo the request, TotalCount equals the seeded
    /// total (no filter applied), and the projection produced non-null DTOs.
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ToPagedResultAsync_Invariants_Hold()
    {
        var gen = from page in Gen.Choose(0, 5)
                  from pageSize in Gen.Choose(1, 10)
                  select (page, pageSize);

        return Prop.ForAll(Arb.From(gen), input =>
        {
            const int seeded = 15;
            using var context = CreateInMemoryContext();
            Seed(context, seeded);

            var query = context.AuditLogEntries.AsNoTracking().AsQueryable();

            var result = query.ToPagedResultAsync(
                input.page,
                input.pageSize,
                sortBy: null,
                sortDescending: true,
                defaultSort: q => q.OrderByDescending(e => e.Timestamp),
                projection: e => new AuditLogEntryDtoStub { Id = e.Id, EntityName = e.EntityName })
                .GetAwaiter().GetResult();

            var sizeOk = result.Items.Count <= input.pageSize;
            var pageOk = result.Page == input.page;
            var pageSizeOk = result.PageSize == input.pageSize;
            var totalOk = result.TotalCount == seeded;
            var projectedOk = result.Items.All(x => x.EntityName is not null);

            return (sizeOk && pageOk && pageSizeOk && totalOk && projectedOk)
                .Label($"page={input.page} size={input.pageSize} items={result.Items.Count} " +
                       $"total={result.TotalCount} sizeOk={sizeOk} pageOk={pageOk} " +
                       $"pageSizeOk={pageSizeOk} totalOk={totalOk} projectedOk={projectedOk}");
        });
    }

    /// <summary>
    /// A minimal projection target used to confirm the helper applies the supplied projection expression.
    /// </summary>
    private sealed class AuditLogEntryDtoStub
    {
        /// <summary>The entry identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>The entity name, used to assert the projection ran.</summary>
        public string? EntityName { get; set; }
    }
}