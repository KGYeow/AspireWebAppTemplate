using AspireWebAppTemplate.Application.Features.Template.AuditLog;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Web.Services;
using AspireWebAppTemplate.Web.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.AuditLog;

/// <summary>
/// Audit log page displaying a searchable, filterable, paginated data grid of audit entries.
/// Requires the "Admin" role for access. Delegates all data operations to the API
/// via <see cref="ApiAuditLogService"/>.
/// </summary>
public partial class Index : ComponentBase, IDisposable
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for querying and exporting audit log entries via the API.
    /// </summary>
    [Inject] private ApiAuditLogService AuditLogService { get; set; } = default!;

    /// <summary>
    /// Provides user-aware datetime formatting and timezone conversion.
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

    /// <summary>
    /// Reference to the MudDateRangePicker for dialog action buttons (Clear, Cancel, OK).
    /// </summary>
    private MudDateRangePicker _picker = null!;

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
    /// Displayed in the user's local time; converted to UTC before querying the API.
    /// Defaults to the last 30 days.
    /// </summary>
    private DateRange? _dateRange = new(DateTime.Today.AddDays(-30), DateTime.Today);

    #endregion

    #region Lifecycle

    /// <summary>
    /// Tracks whether the timezone context is ready for date conversion.
    /// </summary>
    private bool _isReady;

    /// <summary>
    /// Tracks whether the component has been disposed (prerender cleanup).
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Subscribes to the timezone context initialization event.
    /// If the timezone is already initialized (SPA navigation), marks ready immediately.
    /// </summary>
    protected override void OnInitialized()
    {
        // If timezone is already available (navigating from another page), mark ready immediately
        if (!string.IsNullOrEmpty(TimeZoneContext.TimeZoneId))
        {
            _isReady = true;
        }

        // Subscribe to be notified when timezone becomes available (page refresh scenario)
        TimeZoneContext.OnInitialized += OnTimeZoneReady;
    }

    /// <summary>
    /// Triggered when the timezone context is initialized by MainLayout.
    /// Marks the component as ready and reloads the grid with proper date conversion.
    /// </summary>
    private async void OnTimeZoneReady()
    {
        if (_isReady || _disposed) return;
        _isReady = true;
        try
        {
            await InvokeAsync(async () =>
            {
                await _dataGrid.ReloadServerData();
                StateHasChanged();
            });
        }
        catch (ObjectDisposedException)
        {
            // Component was disposed during prerender→circuit transition — safe to ignore
        }
    }

    /// <summary>
    /// Triggers the initial grid load if the timezone was already available.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _isReady)
        {
            await _dataGrid.ReloadServerData();
            StateHasChanged();
        }
    }

    /// <summary>
    /// Unsubscribes from the timezone event to prevent memory leaks.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        TimeZoneContext.OnInitialized -= OnTimeZoneReady;
    }

    #endregion

    #region Server-Side Data Loading

    /// <summary>
    /// Server-side reload callback for <see cref="MudDataGrid{T}"/>.
    /// Calls the API with current filters and pagination parameters,
    /// then maps the response to view models with line numbering.
    /// </summary>
    private async Task<GridData<AuditLogViewModel>> ServerReload(GridState<AuditLogViewModel> state, CancellationToken cancellationToken)
    {
        // Skip during prerender and before timezone context is initialized
        if (!_isReady)
            return new GridData<AuditLogViewModel> { Items = [], TotalItems = 0 };

        IsLoading = true;

        try
        {
            // Use default page size if not yet initialized by the grid
            var pageSize = state.PageSize > 0 ? state.PageSize : 10;

            // Extract sort state from the grid (single-column sort)
            string? sortBy = null;
            var sortDescending = true;
            var firstSort = state.SortDefinitions.FirstOrDefault();
            if (firstSort is not null && !string.IsNullOrWhiteSpace(firstSort.SortBy))
            {
                sortBy = firstSort.SortBy;
                sortDescending = firstSort.Descending;
            }

            // Build query parameters for the API call
            var queryParams = new Application.Features.Template.AuditLog.AuditLogQueryParams
            {
                Page = state.Page,
                PageSize = pageSize,
                SearchTerm = _searchString,
                ActionType = _actionTypeFilter,
                EntityType = _entityTypeFilter,
                DateStart = TimeZoneContext.ConvertToUtc(_dateRange?.Start),
                DateEnd = TimeZoneContext.ConvertToUtc(_dateRange?.End?.Date.AddDays(1).AddTicks(-1)),
                SortBy = sortBy,
                SortDescending = sortDescending
            };

            var apiResult = await AuditLogService.GetPagedAsync(queryParams);

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
                Entry = entry
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
            var exportResult = await AuditLogService.ExportExcelAsync(new Application.Features.Template.AuditLog.AuditLogQueryParams
            {
                SearchTerm = _searchString,
                ActionType = _actionTypeFilter,
                EntityType = _entityTypeFilter,
                DateStart = TimeZoneContext.ConvertToUtc(_dateRange?.Start),
                DateEnd = TimeZoneContext.ConvertToUtc(_dateRange?.End?.Date.AddDays(1).AddTicks(-1))
            });

            if (!exportResult.Succeeded || exportResult.Data is null || exportResult.Data.Length == 0)
            {
                Snackbar.Add("No entries to export.", Severity.Info);
                return;
            }

            var bytes = exportResult.Data;
            var fileName = $"audit-log-{DateTime.UtcNow:yyyy-MM-dd}.xlsx";
            var base64 = Convert.ToBase64String(bytes);
            var mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            var downloadModule = await JSRuntime.InvokeAsync<Microsoft.JSInterop.IJSObjectReference>("import", "./js/download.js");
            await downloadModule.InvokeVoidAsync("downloadFile", fileName, mimeType, base64);
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
    /// Wrapper view model for the audit log data grid.
    /// Holds an <see cref="AuditLogEntryDto"/> reference and delegates properties.
    /// </summary>
    public class AuditLogViewModel
    {
        /// <summary>Display line number (1-based, page-aware).</summary>
        public int LineNumber { get; set; }

        /// <summary>The underlying audit log entry DTO.</summary>
        public AuditLogEntryDto Entry { get; set; } = default!;

        // Delegated from DTO
        public Guid Id => Entry.Id;
        public DateTime Timestamp => Entry.Timestamp;
        public string UserDisplayName => Entry.UserDisplayName;
        public AuditActionType ActionType => Entry.ActionType;
        public AuditEntityType EntityType => Entry.EntityType;
        public string EntityName => Entry.EntityName;
        public string Description => Entry.Description;
    }

    #endregion
}
