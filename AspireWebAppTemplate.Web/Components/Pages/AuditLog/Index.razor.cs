using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.AuditLog;

/// <summary>
/// Audit log page displaying a searchable, filterable, paginated data grid of audit entries.
/// Requires the "Admin" role for access. Delegates all data operations to the API
/// via <see cref="ApiAuditLogService"/>.
/// </summary>
public partial class Index : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for querying and exporting audit log entries via the API.
    /// </summary>
    [Inject] private ApiAuditLogService AuditLogService { get; set; } = default!;

    /// <summary>
    /// Provides user-aware datetime formatting using the current user's configured time zone.
    /// </summary>
    [Inject] private IUserTimeZoneContext TimeZoneContext { get; set; } = default!;

    /// <summary>
    /// JavaScript runtime for triggering browser file downloads.
    /// </summary>
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    #endregion

    #region Data Grid

    /// <summary>
    /// Reference to the MudDataGrid component for triggering server-side reloads.
    /// </summary>
    private MudDataGrid<AuditLogViewModel> _dataGrid = null!;

    #endregion

    #region State

    /// <summary>
    /// Whether the grid data is currently loading.
    /// </summary>
    protected bool IsLoading { get; private set; } = true;

    /// <summary>
    /// Whether a CSV export is currently in progress.
    /// </summary>
    protected bool IsExporting { get; private set; }

    /// <summary>
    /// Tracks the total number of entries matching the current filters.
    /// Used to disable the Export button when zero results are displayed.
    /// </summary>
    private int _totalItems;

    /// <summary>
    /// The current global search term from the toolbar search box.
    /// </summary>
    private string? _searchString;

    /// <summary>
    /// The selected ActionType filter from the toolbar dropdown.
    /// </summary>
    private AuditActionType? _actionTypeFilter;

    /// <summary>
    /// The selected EntityType filter from the toolbar dropdown.
    /// </summary>
    private AuditEntityType? _entityTypeFilter;

    /// <summary>
    /// The date range filter (inclusive on both ends).
    /// Defaults to the last 30 days.
    /// </summary>
    private DateRange _dateRange = new(DateTime.Today.AddDays(-30), DateTime.Today);

    #endregion

    #region Server-Side Data Loading

    /// <summary>
    /// Server-side reload callback for <see cref="MudDataGrid{T}"/>.
    /// Calls the API with current filters and pagination parameters,
    /// then maps the response to view models with line numbering.
    /// </summary>
    private async Task<GridData<AuditLogViewModel>> ServerReload(GridState<AuditLogViewModel> state, CancellationToken cancellationToken)
    {
        IsLoading = true;

        try
        {
            var apiResult = await AuditLogService.GetPagedAsync(
                page: state.Page,
                pageSize: state.PageSize,
                searchTerm: _searchString,
                actionType: _actionTypeFilter,
                entityType: _entityTypeFilter,
                dateStart: _dateRange?.Start,
                dateEnd: _dateRange?.End);

            if (!apiResult.Succeeded || apiResult.Data is null)
            {
                _totalItems = 0;
                return new GridData<AuditLogViewModel> { Items = [], TotalItems = 0 };
            }

            var result = apiResult.Data;
            _totalItems = result.TotalCount;

            var viewModels = result.Items.Select((entry, index) => new AuditLogViewModel
            {
                LineNumber = state.Page * state.PageSize + index + 1,
                Id = entry.Id,
                Timestamp = entry.Timestamp,
                UserDisplayName = entry.UserDisplayName,
                ActionType = entry.ActionType,
                EntityType = entry.EntityType,
                EntityName = entry.EntityName,
                Description = entry.Description
            }).ToList();

            return new GridData<AuditLogViewModel>
            {
                Items = viewModels,
                TotalItems = result.TotalCount
            };
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles the global search text change from the toolbar search box.
    /// </summary>
    private Task OnSearch(string text)
    {
        _searchString = text;
        return _dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Handles ActionType dropdown selection changes.
    /// </summary>
    private Task OnActionTypeChanged(AuditActionType? value)
    {
        _actionTypeFilter = value;
        return _dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Handles EntityType dropdown selection changes.
    /// </summary>
    private Task OnEntityTypeChanged(AuditEntityType? value)
    {
        _entityTypeFilter = value;
        return _dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Handles date range picker changes.
    /// </summary>
    private Task OnDateRangeChanged(DateRange? value)
    {
        _dateRange = value;
        return _dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Opens the <see cref="AuditLogDetailDialog"/> when a row is clicked in the data grid.
    /// Loads the full entry from the API (including OldValues/NewValues).
    /// </summary>
    protected async Task OnRowClick(DataGridRowClickEventArgs<AuditLogViewModel> args)
    {
        var apiResult = await AuditLogService.GetByIdAsync(args.Item.Id);
        if (!apiResult.Succeeded || apiResult.Data is null)
            return;

        var parameters = new DialogParameters<AuditLogDetailDialog>
        {
            { x => x.Entry, apiResult.Data }
        };

        var options = new DialogOptions
        {
            CloseButton = true,
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        };

        await DialogService.ShowAsync<AuditLogDetailDialog>("Audit Log Entry Details", parameters, options);
    }

    #endregion

    #region Export

    /// <summary>
    /// Exports the current filtered audit log entries to an Excel file via the API
    /// and triggers a browser download.
    /// </summary>
    protected async Task ExportAsync()
    {
        if (IsExporting)
            return;

        IsExporting = true;

        try
        {
            var exportResult = await AuditLogService.ExportExcelAsync(
                searchTerm: _searchString,
                actionType: _actionTypeFilter,
                entityType: _entityTypeFilter,
                dateStart: _dateRange?.Start,
                dateEnd: _dateRange?.End);

            if (!exportResult.Succeeded || exportResult.Data is null || exportResult.Data.Length == 0)
            {
                Snackbar.Add("No entries to export.", Severity.Info);
                return;
            }

            var bytes = exportResult.Data;
            var fileName = $"audit-log-{DateTime.UtcNow:yyyy-MM-dd}.xlsx";
            var base64 = Convert.ToBase64String(bytes);
            var mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            await JSRuntime.InvokeVoidAsync(
                "eval",
                $"(function(){{var a=document.createElement('a');a.href='data:{mimeType};base64,{base64}';a.download='{fileName}';document.body.appendChild(a);a.click();document.body.removeChild(a);}})()");
        }
        catch (Exception)
        {
            Snackbar.Add("Failed to export audit log. Please try again.", Severity.Error);
        }
        finally
        {
            IsExporting = false;
        }
    }

    #endregion

    #region View Model

    /// <summary>
    /// Flattened view model for the audit log data grid.
    /// </summary>
    public class AuditLogViewModel
    {
        public int LineNumber { get; set; }
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string UserDisplayName { get; set; } = string.Empty;
        public AuditActionType ActionType { get; set; }
        public AuditEntityType EntityType { get; set; }
        public string EntityName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    #endregion
}
