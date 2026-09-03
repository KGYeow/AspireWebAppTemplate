using AspireWebAppTemplate.Application.Features.Template.Email;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.UI.Utilities;
using AspireWebAppTemplate.Web.Abstractions;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.EmailTemplates;

/// <summary>
/// Admin page for viewing and managing email templates. Displays all templates
/// (system and business) in a data grid. System templates are shown as read-only
/// with a lock icon; business templates have an edit action for content customization.
/// Provides preview functionality for all templates.
/// Uses server-side filtering, sorting, and pagination via <see cref="DataGridHelper{T}"/>.
/// All operations are delegated to the API via <see cref="ApiEmailTemplateService"/>.
/// </summary>
public partial class Index : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for email template operations.
    /// </summary>
    [Inject] private ApiEmailTemplateService EmailTemplateService { get; set; } = default!;

    /// <summary>
    /// Provides user-aware datetime formatting in the viewer's configured time zone.
    /// </summary>
    [Inject] private IUserTimeZoneContext UserTimeZone { get; set; } = default!;

    /// <summary>
    /// Structured logger for diagnostics.
    /// </summary>
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// Reference to the MudDataGrid component for triggering server-side reloads.
    /// </summary>
    private MudDataGrid<EmailTemplateViewModel> _dataGrid = null!;

    /// <summary>
    /// Server-side helper that applies column filters, multi-sort, global search,
    /// and pagination based on <see cref="GridState{T}"/>.
    /// </summary>
    private readonly DataGridHelper<EmailTemplateViewModel> _dataGridUtils = new DataGridHelper<EmailTemplateViewModel>()
        .MapString(nameof(EmailTemplateViewModel.DisplayName), x => x.DisplayName)
        .MapEnum(nameof(EmailTemplateViewModel.Category), x => x.Category)
        .MapBool(nameof(EmailTemplateViewModel.IsActive), x => x.IsActive);

    /// <summary>
    /// Whether data is currently loading from the API.
    /// </summary>
    private bool _isLoading = true;

    /// <summary>
    /// The current global search term for the toolbar search box.
    /// </summary>
    private string? _searchString;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the page. Template data is populated via
    /// <see cref="ServerReload"/> (server-side data grid callback).
    /// </summary>
    protected override Task OnInitializedAsync()
    {
        _isLoading = false;
        return Task.CompletedTask;
    }

    #endregion

    #region Server-Side Data Loading

    /// <summary>
    /// Server-side reload callback for <see cref="MudDataGrid{T}"/>.
    /// Loads all templates from the API, then delegates filtering, sorting, and pagination
    /// to <see cref="DataGridHelper{T}.ServerReloadAsync"/>.
    /// </summary>
    private async Task<GridData<EmailTemplateViewModel>> ServerReload(GridState<EmailTemplateViewModel> state, CancellationToken cancellationToken)
    {
        async Task<IEnumerable<EmailTemplateViewModel>> loader()
        {
            var result = await EmailTemplateService.GetAllAsync();
            if (!result.Succeeded || result.Data is null)
            {
                Logger.LogError("Failed to load email templates: {Error}", result.Error);
                Snackbar.Add("Failed to load email templates.", Severity.Error);
                return [];
            }
            return result.Data.Select(t => new EmailTemplateViewModel { Template = t });
        }

        IEnumerable<string> GlobalFields(EmailTemplateViewModel vm) => new[]
        {
            vm.DisplayName,
            vm.Category.ToString(),
            vm.IsActive ? "Active" : "Inactive"
        };

        void SetLine(EmailTemplateViewModel item, int lineNo) => item.LineNumber = lineNo;

        return await _dataGridUtils.ServerReloadAsync(
            state,
            loader,
            globalSearchTerm: _searchString,
            globalSearchFieldSelector: GlobalFields,
            setLineNumber: SetLine);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles changes to the search text field. Updates the search term and reloads the grid.
    /// </summary>
    private async Task OnSearch(string text)
    {
        _searchString = text;
        await _dataGrid.ReloadServerData();
    }

    /// <summary>
    /// Opens the edit dialog for a business template. On successful save,
    /// reloads the grid to reflect the updated content.
    /// </summary>
    /// <param name="vm">The view model wrapping the business template to edit.</param>
    private async Task OpenEditDialogAsync(EmailTemplateViewModel vm)
    {
        var parameters = new DialogParameters<EditEmailTemplateDialog>
        {
            { x => x.Template, vm.Template }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<EditEmailTemplateDialog>("Edit Email Template", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            Snackbar.Add("Email template updated successfully.", Severity.Success);
            await _dataGrid.ReloadServerData();
        }
    }

    /// <summary>
    /// Opens the preview dialog for any template (system or business).
    /// Renders the template with sample placeholder data and displays the result.
    /// </summary>
    /// <param name="vm">The view model wrapping the template to preview.</param>
    private async Task OpenPreviewDialogAsync(EmailTemplateViewModel vm)
    {
        var parameters = new DialogParameters<PreviewEmailTemplateDialog>
        {
            { x => x.Template, vm.Template }
        };

        var options = new DialogOptions { CloseButton = true, CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        await DialogService.ShowAsync<PreviewEmailTemplateDialog>("Preview Template", parameters, options);
    }

    #endregion

    #region View Model

    /// <summary>
    /// View model wrapping <see cref="EmailTemplateDto"/> with a display line number
    /// for the data grid row index.
    /// </summary>
    private sealed class EmailTemplateViewModel
    {
        /// <summary>Row number displayed in the "#" column.</summary>
        public int LineNumber { get; set; }

        /// <summary>The underlying email template DTO.</summary>
        public EmailTemplateDto Template { get; set; } = default!;

        /// <summary>The unique identifier of the email template.</summary>
        public Guid Id => Template.Id;

        /// <summary>The human-readable display name shown in the admin UI.</summary>
        public string DisplayName => Template.DisplayName;

        /// <summary>The template category (System or Business).</summary>
        public EmailTemplateCategory Category => Template.Category;

        /// <summary>Whether the template is currently active.</summary>
        public bool IsActive => Template.IsActive;

        /// <summary>The UTC timestamp when the template was last updated.</summary>
        public DateTime? UpdatedAtUtc => Template.UpdatedAtUtc;

        /// <summary>Comma-separated list of available placeholder variable names.</summary>
        public string PlaceholderHints => Template.PlaceholderHints;
    }

    #endregion
}
