using System.ComponentModel.DataAnnotations;
using BlazorWebAppTemplate.Abstractions;
using BlazorWebAppTemplate.Core.Domain.Enums;
using BlazorWebAppTemplate.Core.Utilities;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using BlazorWebAppTemplate.UI.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MudBlazor;

namespace BlazorWebAppTemplate.Components.Pages.AuditLog;

/// <summary>
/// Audit log page displaying a searchable, filterable, paginated data grid of audit entries.
/// Requires the "Admin" role for access. Uses <see cref="QueryableDataGridUtils{T}"/> for
/// database-level server-side filtering, sorting, and pagination via EF Core expressions.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the UserManagement page which loads all records into memory, this page operates
/// directly on <see cref="IQueryable{T}"/> to handle potentially millions of audit entries
/// efficiently. Only the current page of data leaves the database.
/// </para>
/// <para>
/// The toolbar provides: global search (500ms debounce), ActionType dropdown, EntityType dropdown,
/// date range pickers, and an Export CSV button. All filters are combined with AND logic.
/// Row clicks open the <see cref="AuditLogDetailDialog"/> for full entry inspection.
/// </para>
/// </remarks>
public partial class Index : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// EF Core database context for querying audit log entries.
    /// </summary>
    [Inject] private ApplicationDbContext DbContext { get; set; } = default!;

    /// <summary>
    /// Audit log service for auxiliary operations.
    /// </summary>
    [Inject] private IAuditLogService AuditLogService { get; set; } = default!;

    /// <summary>
    /// Provides user-aware datetime formatting using the current user's configured time zone.
    /// </summary>
    [Inject] private IUserTimeZoneContext TimeZoneContext { get; set; } = default!;

    /// <summary>
    /// Excel export service for generating .xlsx files from data collections.
    /// </summary>
    [Inject] private IExcelExportService ExcelExport { get; set; } = default!;

    /// <summary>
    /// JavaScript runtime for triggering browser file downloads.
    /// </summary>
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    #endregion

    #region Server-Side Data Grid

    /// <summary>
    /// Reference to the MudDataGrid component for triggering server-side reloads.
    /// </summary>
    private MudDataGrid<AuditLogViewModel> _dataGrid = null!;

    /// <summary>
    /// Database-level grid utility that translates GridState into EF Core IQueryable expressions.
    /// Maps each filterable/sortable column to its corresponding expression selector on <see cref="AuditLogEntry"/>.
    /// The first DateTime property registered (Timestamp) becomes the default sort field (descending).
    /// </summary>
    private readonly QueryableDataGridUtils<AuditLogEntry> _queryableGridUtils = new QueryableDataGridUtils<AuditLogEntry>()
        .MapString(nameof(AuditLogEntry.UserDisplayName), x => x.UserDisplayName)
        .MapString(nameof(AuditLogEntry.EntityName), x => x.EntityName)
        .MapString(nameof(AuditLogEntry.Description), x => x.Description)
        .MapDateTime(nameof(AuditLogEntry.Timestamp), x => x.Timestamp);

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
    /// Used to disable the Export CSV button when zero results are displayed.
    /// </summary>
    private int _totalItems;

    /// <summary>
    /// The current global search term from the toolbar search box.
    /// Applied across UserDisplayName, EntityName, and Description fields (case-insensitive).
    /// </summary>
    private string? _searchString;

    /// <summary>
    /// The selected ActionType filter from the toolbar dropdown.
    /// When null, no ActionType filter is applied.
    /// </summary>
    private AuditActionType? _actionTypeFilter;

    /// <summary>
    /// The selected EntityType filter from the toolbar dropdown.
    /// When null, no EntityType filter is applied.
    /// </summary>
    private AuditEntityType? _entityTypeFilter;

    /// <summary>
    /// The date range filter (inclusive on both ends).
    /// Defaults to the last 30 days to show recent activity on initial load.
    /// </summary>
    private DateRange _dateRange = new(DateTime.Today.AddDays(-30), DateTime.Today);

    #endregion

    #region Server-Side Data Loading

    /// <summary>
    /// Server-side reload callback for <see cref="MudDataGrid{T}"/>.
    /// Builds the base query from the database, applies toolbar filters (ActionType, EntityType, date range),
    /// then delegates column filtering, global search, sorting, and pagination to
    /// <see cref="QueryableDataGridUtils{T}.ServerReloadAsync"/>.
    /// Results are mapped from <see cref="AuditLogEntry"/> to <see cref="AuditLogViewModel"/>.
    /// </summary>
    private async Task<GridData<AuditLogViewModel>> ServerReload(GridState<AuditLogViewModel> state, CancellationToken cancellationToken)
    {
        IsLoading = true;

        try
        {
            // Start with the base query on the AuditLogEntries table
            var query = DbContext.AuditLogEntries.AsNoTracking().AsQueryable();

            // Apply toolbar filters before passing to the grid utility (AND logic)
            if (_actionTypeFilter.HasValue)
                query = query.Where(x => x.ActionType == _actionTypeFilter.Value);

            if (_entityTypeFilter.HasValue)
                query = query.Where(x => x.EntityType == _entityTypeFilter.Value);

            // Date range filtering: inclusive on both ends
            if (_dateRange?.Start.HasValue == true)
                query = query.Where(x => x.Timestamp >= _dateRange.Start.Value.Date);

            if (_dateRange?.End.HasValue == true)
                query = query.Where(x => x.Timestamp <= _dateRange.End.Value.Date.AddDays(1).AddTicks(-1));

            // Translate the MudDataGrid GridState<AuditLogViewModel> to GridState<AuditLogEntry>
            // Since the grid is bound to AuditLogViewModel but the query is on AuditLogEntry,
            // we create a compatible state for the utility
            var entryState = new GridState<AuditLogEntry>
            {
                Page = state.Page,
                PageSize = state.PageSize,
                SortDefinitions = MapSortDefinitions(state.SortDefinitions),
                FilterDefinitions = new List<IFilterDefinition<AuditLogEntry>>()
            };

            // Delegate to the queryable grid utility for database-level filtering, sorting, pagination
            var gridData = await _queryableGridUtils.ServerReloadAsync(
                query,
                entryState,
                globalSearchTerm: _searchString,
                globalSearchFields: new[] { nameof(AuditLogEntry.UserDisplayName), nameof(AuditLogEntry.EntityName), nameof(AuditLogEntry.Description) });

            // Track total items for Export CSV button state (disabled when zero results)
            _totalItems = gridData.TotalItems;

            // Map AuditLogEntry items to AuditLogViewModel with page-aware line numbering
            var viewModels = gridData.Items.Select((entry, index) => new AuditLogViewModel
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
                TotalItems = gridData.TotalItems
            };
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Maps sort definitions from the view model grid state to the entity grid state.
    /// Property names are the same between <see cref="AuditLogViewModel"/> and <see cref="AuditLogEntry"/>
    /// so this is a direct translation of the sort configuration.
    /// </summary>
    private static ICollection<SortDefinition<AuditLogEntry>> MapSortDefinitions(
        ICollection<SortDefinition<AuditLogViewModel>> viewModelSorts)
    {
        if (viewModelSorts is null || viewModelSorts.Count == 0)
            return new List<SortDefinition<AuditLogEntry>>();

        return viewModelSorts.Select(s => new SortDefinition<AuditLogEntry>(
            s.SortBy,
            s.Descending,
            s.Index,
            null)).ToList();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles the global search text change from the toolbar search box.
    /// Triggers a server-side reload with the updated search term.
    /// </summary>
    private Task OnSearch(string text)
    {
        _searchString = text;
        return _dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Handles ActionType dropdown selection changes.
    /// Triggers a server-side reload with the updated ActionType filter.
    /// </summary>
    private Task OnActionTypeChanged(AuditActionType? value)
    {
        _actionTypeFilter = value;
        return _dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Handles EntityType dropdown selection changes.
    /// Triggers a server-side reload with the updated EntityType filter.
    /// </summary>
    private Task OnEntityTypeChanged(AuditEntityType? value)
    {
        _entityTypeFilter = value;
        return _dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Handles date range picker changes.
    /// Triggers a server-side reload with the updated date range.
    /// </summary>
    private Task OnDateRangeChanged(DateRange? value)
    {
        _dateRange = value;
        return _dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Opens the <see cref="AuditLogDetailDialog"/> when a row is clicked in the data grid.
    /// Loads the full <see cref="AuditLogEntry"/> from the database (including OldValues/NewValues)
    /// and displays it in a modal dialog.
    /// </summary>
    protected async Task OnRowClick(DataGridRowClickEventArgs<AuditLogViewModel> args)
    {
        // Load the full entity from the database for the detail view (includes OldValues/NewValues/IpAddress)
        var entry = await DbContext.AuditLogEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == args.Item.Id);

        if (entry is null)
            return;

        var parameters = new DialogParameters<AuditLogDetailDialog>
        {
            { x => x.Entry, entry }
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
    /// Exports the current filtered audit log entries to an Excel file and triggers a browser download.
    /// Uses <see cref="QueryableDataGridUtils{T}.GetAllMatchingAsync"/> with current filters,
    /// capped at 50,000 rows to prevent memory exhaustion.
    /// </summary>
    protected async Task ExportAsync()
    {
        if (IsExporting)
            return;

        IsExporting = true;

        try
        {
            // Build the same filtered query as ServerReload
            var query = DbContext.AuditLogEntries.AsNoTracking().AsQueryable();

            if (_actionTypeFilter.HasValue)
                query = query.Where(x => x.ActionType == _actionTypeFilter.Value);

            if (_entityTypeFilter.HasValue)
                query = query.Where(x => x.EntityType == _entityTypeFilter.Value);

            if (_dateRange?.Start.HasValue == true)
                query = query.Where(x => x.Timestamp >= _dateRange.Start.Value.Date);

            if (_dateRange?.End.HasValue == true)
                query = query.Where(x => x.Timestamp <= _dateRange.End.Value.Date.AddDays(1).AddTicks(-1));

            var emptyState = new GridState<AuditLogEntry>
            {
                Page = 0,
                PageSize = 50_000,
                SortDefinitions = new List<SortDefinition<AuditLogEntry>>(),
                FilterDefinitions = new List<IFilterDefinition<AuditLogEntry>>()
            };

            // Row cap: 50,000 rows maximum
            var entries = await _queryableGridUtils.GetAllMatchingAsync(
                query,
                emptyState,
                globalSearchTerm: _searchString,
                globalSearchFields: new[] { nameof(AuditLogEntry.UserDisplayName), nameof(AuditLogEntry.EntityName), nameof(AuditLogEntry.Description) },
                maxRows: 50_000);

            if (entries.Count == 0)
            {
                Snackbar.Add("No entries to export.", Severity.Info);
                return;
            }

            // Map to export DTO with formatted columns
            var exportData = entries.Select(e => new AuditLogExportRow
            {
                Timestamp = e.Timestamp,
                User = e.UserDisplayName,
                ActionType = e.ActionType.ToString(),
                EntityType = e.EntityType.ToString(),
                EntityName = e.EntityName,
                Description = e.Description,
                IpAddress = e.IpAddress ?? string.Empty
            });

            // Generate Excel file via the export service
            var bytes = ExcelExport.ExportToExcel(exportData, "Audit Log");

            // Trigger browser download
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

    #region Export DTO

    /// <summary>
    /// Flat DTO for audit log Excel export with column configuration via <see cref="ExportColumnAttribute"/>.
    /// </summary>
    private sealed class AuditLogExportRow
    {
        [ExportColumn(1)]
        [Display(Name = "Timestamp")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}")]
        public DateTime Timestamp { get; set; }

        [ExportColumn(2)]
        [Display(Name = "User")]
        public string User { get; set; } = string.Empty;

        [ExportColumn(3)]
        [Display(Name = "Action Type")]
        public string ActionType { get; set; } = string.Empty;

        [ExportColumn(4)]
        [Display(Name = "Entity Type")]
        public string EntityType { get; set; } = string.Empty;

        [ExportColumn(5)]
        [Display(Name = "Entity Name")]
        public string EntityName { get; set; } = string.Empty;

        [ExportColumn(6)]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [ExportColumn(7, NullText = "N/A")]
        [Display(Name = "IP Address")]
        public string IpAddress { get; set; } = string.Empty;
    }

    #endregion

    #region View Model

    /// <summary>
    /// Flattened view model for the audit log data grid.
    /// Properties are mapped to <see cref="QueryableDataGridUtils{T}"/>
    /// for server-side filtering, sorting, and pagination support.
    /// </summary>
    public class AuditLogViewModel
    {
        /// <summary>Display line number (1-based, page-aware).</summary>
        public int LineNumber { get; set; }

        /// <summary>The unique identifier of the audit log entry.</summary>
        public Guid Id { get; set; }

        /// <summary>The UTC timestamp when the audited action occurred.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>The display name of the user who performed the action.</summary>
        public string UserDisplayName { get; set; } = string.Empty;

        /// <summary>The category of action that was performed.</summary>
        public AuditActionType ActionType { get; set; }

        /// <summary>The type of entity affected by the action.</summary>
        public AuditEntityType EntityType { get; set; }

        /// <summary>The human-readable name of the entity affected.</summary>
        public string EntityName { get; set; } = string.Empty;

        /// <summary>A brief human-readable summary of the action.</summary>
        public string Description { get; set; } = string.Empty;
    }

    #endregion
}
