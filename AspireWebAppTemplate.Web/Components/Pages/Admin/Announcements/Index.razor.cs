using AspireWebAppTemplate.Application.Contracts.Announcements;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.UI.Components.Shared;
using AspireWebAppTemplate.UI.Utilities;
using AspireWebAppTemplate.Web.Abstractions;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.Announcements;

/// <summary>
/// Admin management page for announcements. Provides full CRUD operations
/// with a global search box, grid-native column filtering, and bulk delete.
/// Uses MudDataGrid with server-side filtering, sorting, and pagination
/// via <see cref="DataGridUtils{T}"/>, matching the User/Role Management pattern.
/// All operations are delegated to the API via <see cref="ApiAnnouncementService"/>.
/// </summary>
public partial class Index : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for announcement operations.
    /// </summary>
    [Inject] private ApiAnnouncementService AnnouncementService { get; set; } = default!;

    /// <summary>
    /// Provides user-aware datetime formatting in the viewer's configured time zone.
    /// </summary>
    [Inject] private IUserTimeZoneContext UserTimeZone { get; set; } = default!;

    /// <summary>
    /// Structured logger.
    /// </summary>
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    #endregion

    #region Server-Side Data Grid

    /// <summary>
    /// Reference to the MudDataGrid component for triggering server-side reloads.
    /// </summary>
    private MudDataGrid<AnnouncementViewModel> _dataGrid = null!;

    /// <summary>
    /// Server-side helper that applies column filters, multi-sort, global search,
    /// and pagination based on <see cref="GridState{T}"/>.
    /// Maps each filterable/sortable column to its corresponding property selector.
    /// </summary>
    private readonly DataGridUtils<AnnouncementViewModel> _dataGridUtils = new DataGridUtils<AnnouncementViewModel>()
        .MapString(nameof(AnnouncementViewModel.Title), x => x.Title)
        .MapEnum(nameof(AnnouncementViewModel.DisplayType), x => x.DisplayType)
        .MapEnum(nameof(AnnouncementViewModel.Severity), x => x.Severity)
        .MapString(nameof(AnnouncementViewModel.Status), x => x.Status)
        .MapDateTime(nameof(AnnouncementViewModel.StartsAtUtc), x => x.StartsAtUtc)
        .MapDateTime(nameof(AnnouncementViewModel.ExpiresAtUtc), x => x.ExpiresAtUtc)
        .MapDateTime(nameof(AnnouncementViewModel.CreatedAtUtc), x => x.CreatedAtUtc);

    #endregion

    #region Bulk Actions State

    /// <summary>
    /// The set of currently selected announcements in the data grid.
    /// Bound via <c>@bind-SelectedItems</c>.
    /// </summary>
    private HashSet<AnnouncementViewModel> _selectedItems = new();

    #endregion

    #region State

    /// <summary>
    /// Whether data is currently loading. Driven by the grid's ServerData callback.
    /// Initialized to false so the Create button is enabled on first render.
    /// </summary>
    private bool _isLoading;

    /// <summary>
    /// The current global search term for the toolbar search box.
    /// Applied across Title and Message fields via <see cref="DataGridUtils{T}"/>.
    /// </summary>
    private string? _searchString;

    #endregion

    #region Server-Side Data Loading

    /// <summary>
    /// Server-side reload callback for <see cref="MudDataGrid{T}"/>.
    /// Loads all announcements from the API, then delegates column filtering,
    /// sorting, and pagination to <see cref="DataGridUtils{T}.ServerReloadAsync"/>.
    /// </summary>
    private async Task<GridData<AnnouncementViewModel>> ServerReload(GridState<AnnouncementViewModel> state, CancellationToken cancellationToken)
    {
        _isLoading = true;

        try
        {
            // Loader function: fetches all announcements from API and wraps in ViewModels
            async Task<IEnumerable<AnnouncementViewModel>> loader()
            {
                var result = await AnnouncementService.GetAllAsync();
                return result.Succeeded
                    ? (result.Data ?? []).Select(a => new AnnouncementViewModel { Announcement = a })
                    : [];
            }

            // Global search fields — searches across all visible columns using the same
            // formatted values displayed in the grid (timezone-converted dates)
            IEnumerable<string> GlobalFields(AnnouncementViewModel vm) => new[]
            {
                vm.Title,
                vm.DisplayType.ToString(),
                vm.Severity.ToString(),
                vm.Status,
                UserTimeZone.FormatDateTime(vm.StartsAtUtc),
                UserTimeZone.FormatDateTime(vm.ExpiresAtUtc),
                UserTimeZone.FormatDateTime(vm.CreatedAtUtc)
            };

            void SetLine(AnnouncementViewModel item, int lineNo) => item.LineNumber = lineNo;

            return await _dataGridUtils.ServerReloadAsync(
                state,
                loader,
                globalSearchTerm: _searchString,
                globalSearchFieldSelector: GlobalFields,
                setLineNumber: SetLine);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading announcements.");
            Snackbar.Add("An unexpected error occurred while loading announcements.", Severity.Error);
            return new GridData<AnnouncementViewModel> { Items = [], TotalItems = 0 };
        }
        finally
        {
            _isLoading = false;
        }
    }

    #endregion

    #region Bulk Actions

    /// <summary>
    /// Clears the current selection without reloading the grid.
    /// </summary>
    private void ClearSelection()
    {
        _selectedItems = new HashSet<AnnouncementViewModel>();
    }

    /// <summary>
    /// Clears the current selection and reloads the grid.
    /// </summary>
    private async Task ClearSelectionAndReloadAsync()
    {
        _selectedItems = new HashSet<AnnouncementViewModel>();
        await _dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Deletes all selected announcements after confirmation.
    /// </summary>
    private async Task BulkDeleteAsync()
    {
        if (_selectedItems.Count == 0) return;

        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, $"Are you sure you want to delete {_selectedItems.Count} announcement(s)? This action cannot be undone." },
            { x => x.SubmitBtnText, "Delete" },
            { x => x.DialogIcon, Icons.Material.Rounded.DeleteForever },
            { x => x.DialogIconColor, Color.Error }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Delete Announcements", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        int success = 0, failed = 0;
        foreach (var vm in _selectedItems)
        {
            var deleteResult = await AnnouncementService.DeleteAsync(vm.Id);
            if (deleteResult.Succeeded) success++;
            else failed++;
        }

        if (failed == 0)
            Snackbar.Add($"{success} announcement(s) deleted successfully.", Severity.Success);
        else
            Snackbar.Add($"{success} deleted, {failed} failed.", Severity.Warning);

        await ClearSelectionAndReloadAsync();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles the global search text change from the toolbar search box.
    /// </summary>
    private async Task OnSearch(string text)
    {
        _searchString = text;
        await _dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Opens the create announcement dialog.
    /// </summary>
    private async Task OpenCreateDialogAsync()
    {
        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<AnnouncementFormDialog>("Create Announcement", options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            Snackbar.Add("Announcement created successfully.", Severity.Success);
            await _dataGrid.ReloadServerData();
        }
    }

    /// <summary>
    /// Opens the edit announcement dialog pre-populated with the selected announcement's values.
    /// </summary>
    private async Task OpenEditDialogAsync(AnnouncementViewModel vm)
    {
        var parameters = new DialogParameters<AnnouncementFormDialog>
        {
            { x => x.ExistingAnnouncement, vm.Announcement }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<AnnouncementFormDialog>("Edit Announcement", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            Snackbar.Add("Announcement updated successfully.", Severity.Success);
            await _dataGrid.ReloadServerData();
        }
    }

    /// <summary>
    /// Shows a confirmation dialog and deletes the announcement if confirmed.
    /// </summary>
    private async Task DeleteAnnouncementAsync(AnnouncementViewModel vm)
    {
        var parameters = new DialogParameters<ConfirmationDialog>
        {
            { x => x.ContentText, $"Are you sure you want to delete the announcement '{vm.Title}'? This action cannot be undone." },
            { x => x.SubmitBtnText, "Delete" },
            { x => x.DialogIcon, Icons.Material.Rounded.DeleteForever },
            { x => x.DialogIconColor, Color.Error }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Delete Announcement", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        var deleteResult = await AnnouncementService.DeleteAsync(vm.Id);
        if (deleteResult.Succeeded)
        {
            Snackbar.Add("Announcement deleted successfully.", Severity.Success);
            await _dataGrid.ReloadServerData();
        }
        else
        {
            Snackbar.Add(deleteResult.Error ?? "Failed to delete announcement.", Severity.Error);
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Returns the appropriate MudBlazor color for an announcement severity.
    /// </summary>
    private static Color GetSeverityColor(AnnouncementSeverity severity) => severity switch
    {
        AnnouncementSeverity.Critical => Color.Error,
        AnnouncementSeverity.Warning => Color.Warning,
        AnnouncementSeverity.Info => Color.Info,
        _ => Color.Default
    };

    /// <summary>
    /// Returns the appropriate MudBlazor color for an announcement status.
    /// </summary>
    private static Color GetStatusColor(string status) => status switch
    {
        "Active" => Color.Success,
        "Scheduled" => Color.Info,
        "Expired" => Color.Default,
        "Draft" => Color.Default,
        _ => Color.Default
    };

    #endregion

    #region View Model

    /// <summary>
    /// View model wrapping <see cref="AnnouncementDto"/> with a display line number.
    /// </summary>
    private sealed class AnnouncementViewModel
    {
        /// <summary>Row number displayed in the "#" column.</summary>
        public int LineNumber { get; set; }

        /// <summary>The underlying announcement DTO.</summary>
        public AnnouncementDto Announcement { get; set; } = default!;

        /// <summary>The unique identifier of the announcement.</summary>
        public Guid Id => Announcement.Id;

        /// <summary>The plain-text title of the announcement.</summary>
        public string Title => Announcement.Title;

        /// <summary>The display type controlling where the announcement is surfaced.</summary>
        public AnnouncementDisplayType DisplayType => Announcement.DisplayType;

        /// <summary>The severity level indicating announcement urgency.</summary>
        public AnnouncementSeverity Severity => Announcement.Severity;

        /// <summary>The computed status of the announcement.</summary>
        public string Status => Announcement.Status;

        /// <summary>The optional UTC timestamp when the announcement becomes active.</summary>
        public DateTime? StartsAtUtc => Announcement.StartsAtUtc;

        /// <summary>The optional UTC timestamp when the announcement expires.</summary>
        public DateTime? ExpiresAtUtc => Announcement.ExpiresAtUtc;

        /// <summary>The UTC timestamp when the announcement was created.</summary>
        public DateTime CreatedAtUtc => Announcement.CreatedAtUtc;

        /// <summary>Whether the announcement is manually activated.</summary>
        public bool IsActive => Announcement.IsActive;

        /// <summary>
        /// Determines equality by comparing the announcement's unique identifier.
        /// Required for MudDataGrid multi-selection to track items correctly across reloads.
        /// </summary>
        public override bool Equals(object? obj) => obj is AnnouncementViewModel other && Id == other.Id;

        /// <summary>
        /// Returns a hash code based on the announcement's unique identifier.
        /// </summary>
        public override int GetHashCode() => Id.GetHashCode();
    }

    #endregion
}
