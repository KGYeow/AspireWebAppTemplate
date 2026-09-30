// Feature: server-datagrid-helper: composable database-level ServerData helpers
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Web.Utilities;
using MudBlazor;

namespace AspireWebAppTemplate.Tests.Web;

/// <summary>
/// Unit tests for <see cref="ServerDataGridHelper"/> verifying the composable database-level grid
/// helpers: page-size guard, single-column sort extraction, and PagedResult -> GridData mapping with
/// page-aware line numbering (including the failure-closure and line-number-offset behaviors).
/// </summary>
public class ServerDataGridHelperTests
{
    #region Test Types

    /// <summary>A minimal DTO stand-in returned by a fake service.</summary>
    private sealed record SampleDto(int Id, string Name);

    /// <summary>A minimal view-model stand-in produced by the projector.</summary>
    private sealed class SampleViewModel
    {
        public int LineNumber { get; set; }
        public SampleDto Dto { get; set; } = default!;
    }

    #endregion

    #region Helpers

    /// <summary>Builds a <see cref="GridState{T}"/> with the given page, page size, and optional sort.</summary>
    private static GridState<SampleViewModel> State(int page, int pageSize, SortDefinition<SampleViewModel>? sort = null)
    {
        var state = new GridState<SampleViewModel> { Page = page, PageSize = pageSize };
        if (sort is not null)
            state.SortDefinitions = new List<SortDefinition<SampleViewModel>> { sort };
        return state;
    }

    /// <summary>Builds a sort definition; the sort func is unused by the helper and returns null.</summary>
    private static SortDefinition<SampleViewModel> Sort(string sortBy, bool descending)
        => new(sortBy, descending, 0, _ => null!);

    /// <summary>Projects a DTO plus its line number into the view-model.</summary>
    private static SampleViewModel Project(SampleDto dto, int lineNumber)
        => new() { LineNumber = lineNumber, Dto = dto };

    #endregion

    #region ResolvePageSize

    /// <summary>
    /// Property 1: WHEN PageSize is 0 (uninitialized), ResolvePageSize returns the default.
    /// </summary>
    [Fact]
    public void ResolvePageSize_WhenZero_ReturnsDefault()
    {
        var result = ServerDataGridHelper.ResolvePageSize(State(0, 0), defaultPageSize: 10);
        Assert.Equal(10, result);
    }

    /// <summary>
    /// Property 1: WHEN PageSize is positive, ResolvePageSize returns it unchanged.
    /// </summary>
    [Fact]
    public void ResolvePageSize_WhenPositive_ReturnsValue()
    {
        var result = ServerDataGridHelper.ResolvePageSize(State(2, 25), defaultPageSize: 10);
        Assert.Equal(25, result);
    }

    #endregion

    #region ExtractSort

    /// <summary>
    /// Property 5: WHEN no sort definition is present, ExtractSort returns (null, true).
    /// </summary>
    [Fact]
    public void ExtractSort_WhenNoSort_ReturnsNullDescendingTrue()
    {
        var (sortBy, descending) = ServerDataGridHelper.ExtractSort(State(0, 10));
        Assert.Null(sortBy);
        Assert.True(descending);
    }

    /// <summary>
    /// Property 5: WHEN a sort definition is present, ExtractSort returns its SortBy and Descending.
    /// </summary>
    [Theory]
    [InlineData("Name", true)]
    [InlineData("Id", false)]
    public void ExtractSort_WhenSortPresent_ReturnsDefinition(string sortBy, bool descending)
    {
        var (actualSortBy, actualDescending) =
            ServerDataGridHelper.ExtractSort(State(0, 10, Sort(sortBy, descending)));
        Assert.Equal(sortBy, actualSortBy);
        Assert.Equal(descending, actualDescending);
    }

    #endregion

    #region ToGridData - failure closure

    /// <summary>
    /// Property 3: a null ApiResult yields an empty grid and never invokes the projector.
    /// </summary>
    [Fact]
    public void ToGridData_WhenNullResult_ReturnsEmpty_AndDoesNotProject()
    {
        var projectorCalled = false;
        var grid = ServerDataGridHelper.ToGridData<SampleViewModel, SampleDto>(
            null, page: 0, pageSize: 10, (dto, ln) => { projectorCalled = true; return Project(dto, ln); });

        Assert.Empty(grid.Items);
        Assert.Equal(0, grid.TotalItems);
        Assert.False(projectorCalled);
    }

    /// <summary>
    /// Property 3: an unsuccessful ApiResult yields an empty grid and never invokes the projector.
    /// </summary>
    [Fact]
    public void ToGridData_WhenFailure_ReturnsEmpty_AndDoesNotProject()
    {
        var projectorCalled = false;
        var failure = ApiResult<PagedResult<SampleDto>>.Failure("boom");

        var grid = ServerDataGridHelper.ToGridData(
            failure, page: 0, pageSize: 10, (SampleDto dto, int ln) => { projectorCalled = true; return Project(dto, ln); });

        Assert.Empty(grid.Items);
        Assert.Equal(0, grid.TotalItems);
        Assert.False(projectorCalled);
    }

    #endregion

    #region ToGridData - success

    /// <summary>
    /// Property 4: on success, TotalItems equals PagedResult.TotalCount and items are projected.
    /// </summary>
    [Fact]
    public void ToGridData_WhenSuccess_MapsItems_AndTotalMatches()
    {
        var paged = new PagedResult<SampleDto>
        {
            Items = new List<SampleDto> { new(1, "a"), new(2, "b") },
            TotalCount = 42,
            Page = 0,
            PageSize = 10
        };
        var success = ApiResult<PagedResult<SampleDto>>.Success(paged);

        var grid = ServerDataGridHelper.ToGridData(success, page: 0, pageSize: 10, Project);

        var items = grid.Items.ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(42, grid.TotalItems);
        Assert.Equal("a", items[0].Dto.Name);
        Assert.Equal("b", items[1].Dto.Name);
    }

    /// <summary>
    /// Property 2: line numbers are 1-based and page-aware. On page 2 (index 2) with page size 10,
    /// the first row is number 21.
    /// </summary>
    [Fact]
    public void ToGridData_LineNumbers_ArePageAware()
    {
        var paged = new PagedResult<SampleDto>
        {
            Items = new List<SampleDto> { new(1, "a"), new(2, "b"), new(3, "c") },
            TotalCount = 100,
            Page = 2,
            PageSize = 10
        };
        var success = ApiResult<PagedResult<SampleDto>>.Success(paged);

        var grid = ServerDataGridHelper.ToGridData(success, page: 2, pageSize: 10, Project);

        var items = grid.Items.ToList();
        Assert.Equal(21, items[0].LineNumber);
        Assert.Equal(22, items[1].LineNumber);
        Assert.Equal(23, items[2].LineNumber);
    }

    /// <summary>
    /// Property 2 (regression): the caller passes the RESOLVED page size, so line numbers stay correct
    /// even when the raw GridState.PageSize was 0. Here the resolved size (10) drives the offset on page 1.
    /// </summary>
    [Fact]
    public void ToGridData_LineNumbers_UseResolvedPageSize_NotRawZero()
    {
        // Simulate the resolved flow: ResolvePageSize would have turned a raw 0 into 10.
        var resolvedPageSize = ServerDataGridHelper.ResolvePageSize(State(1, 0), defaultPageSize: 10);
        Assert.Equal(10, resolvedPageSize);

        var paged = new PagedResult<SampleDto>
        {
            Items = new List<SampleDto> { new(1, "a"), new(2, "b") },
            TotalCount = 50,
            Page = 1,
            PageSize = resolvedPageSize
        };
        var success = ApiResult<PagedResult<SampleDto>>.Success(paged);

        var grid = ServerDataGridHelper.ToGridData(success, page: 1, pageSize: resolvedPageSize, Project);

        var items = grid.Items.ToList();
        // With the raw 0 the offset would have been 0 (rows 1,2); with the resolved size it is 11,12.
        Assert.Equal(11, items[0].LineNumber);
        Assert.Equal(12, items[1].LineNumber);
    }

    #endregion

    #region Empty

    /// <summary>
    /// Empty returns a grid with no items and zero total.
    /// </summary>
    [Fact]
    public void Empty_ReturnsNoItemsZeroTotal()
    {
        var grid = ServerDataGridHelper.Empty<SampleViewModel>();
        Assert.Empty(grid.Items);
        Assert.Equal(0, grid.TotalItems);
    }

    #endregion
}