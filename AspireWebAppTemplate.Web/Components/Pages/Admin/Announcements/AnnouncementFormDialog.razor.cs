using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Application.Features.Template.Announcements;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Web.Abstractions;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.Announcements;

/// <summary>
/// Reusable form dialog for creating and editing announcements.
/// When <see cref="ExistingAnnouncement"/> is provided, operates in edit mode with pre-populated values.
/// Uses Radzen HtmlEditor for the content/message field.
/// Dates are displayed and entered in the user's local time zone, then converted to UTC for the API.
/// </summary>
public partial class AnnouncementFormDialog : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for announcement operations.
    /// </summary>
    [Inject] private ApiAnnouncementService AnnouncementService { get; set; } = default!;

    /// <summary>
    /// Per-circuit time zone context providing the user's configured IANA time zone ID.
    /// Used to convert between local display time and UTC for API submission.
    /// </summary>
    [Inject] private IUserTimeZoneContext UserTimeZoneContext { get; set; } = default!;

    /// <summary>
    /// Structured logger.
    /// </summary>
    [Inject] private ILogger<AnnouncementFormDialog> Logger { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// The MudBlazor dialog instance.
    /// </summary>
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    #endregion

    #region Parameters

    /// <summary>
    /// The existing announcement to edit. When null, the dialog operates in create mode.
    /// </summary>
    [Parameter]
    public AnnouncementDto? ExistingAnnouncement { get; set; }

    #endregion

    #region State

    /// <summary>
    /// Whether the dialog is in edit mode (true) or create mode (false).
    /// </summary>
    private bool _isEditMode;

    /// <summary>
    /// The form input model.
    /// </summary>
    private InputModel _model = new();

    /// <summary>
    /// Drives the EditForm validation context.
    /// </summary>
    private EditContext _editContext = default!;

    /// <summary>
    /// Controls the submit button's disabled state during save operations.
    /// </summary>
    private bool _isBusy;

    /// <summary>
    /// Status message displayed on validation or server errors.
    /// </summary>
    private string? _statusMessage;

    /// <summary>
    /// Whether the content field is in an error state (empty on submit attempt).
    /// </summary>
    private bool _contentHasError;

    /// <summary>
    /// Whether to clear all dismissal records on update (edit mode only).
    /// </summary>
    private bool _clearDismissals;

    /// <summary>
    /// Date portion of StartsAt for the date picker (in user's local time zone).
    /// </summary>
    private DateTime? _startsAtDate;

    /// <summary>
    /// Time portion of StartsAt for the time picker (in user's local time zone).
    /// </summary>
    private TimeSpan? _startsAtTime;

    /// <summary>
    /// Date portion of ExpiresAt for the date picker (in user's local time zone).
    /// </summary>
    private DateTime? _expiresAtDate;

    /// <summary>
    /// Time portion of ExpiresAt for the time picker (in user's local time zone).
    /// </summary>
    private TimeSpan? _expiresAtTime;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the form. Pre-populates values if editing an existing announcement.
    /// Converts UTC dates from the API to the user's local time zone for display.
    /// </summary>
    protected override void OnInitialized()
    {
        _isEditMode = ExistingAnnouncement is not null;

        if (_isEditMode && ExistingAnnouncement is not null)
        {
            _model = new InputModel
            {
                Title = ExistingAnnouncement.Title,
                Message = ExistingAnnouncement.Message,
                DisplayType = ExistingAnnouncement.DisplayType,
                Severity = ExistingAnnouncement.Severity,
                IsActive = ExistingAnnouncement.IsActive,
                NotifyUsers = ExistingAnnouncement.NotifyUsers
            };

            // Convert UTC → local for date picker display
            var localStartsAt = UserTimeZoneContext.ConvertFromUtc(ExistingAnnouncement.StartsAtUtc);
            if (localStartsAt.HasValue)
            {
                _startsAtDate = localStartsAt.Value.Date;
                _startsAtTime = localStartsAt.Value.TimeOfDay;
            }

            var localExpiresAt = UserTimeZoneContext.ConvertFromUtc(ExistingAnnouncement.ExpiresAtUtc);
            if (localExpiresAt.HasValue)
            {
                _expiresAtDate = localExpiresAt.Value.Date;
                _expiresAtTime = localExpiresAt.Value.TimeOfDay;
            }
        }
        else
        {
            // Default: NotifyUsers is true for Standard, false for Banner
            _model.NotifyUsers = _model.DisplayType == AnnouncementDisplayType.Standard;
        }

        _editContext = new EditContext(_model);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles display type changes — toggles the NotifyUsers default accordingly.
    /// Banner display type defaults NotifyUsers to false, Standard to true.
    /// </summary>
    private void OnDisplayTypeChanged(AnnouncementDisplayType newType)
    {
        _model.DisplayType = newType;
        _model.NotifyUsers = newType == AnnouncementDisplayType.Standard;
    }

    /// <summary>
    /// Validates and submits the form to create or update an announcement.
    /// Converts local date/time picker values to UTC before sending to the API.
    /// On validation errors from the server, displays them inline without closing the dialog.
    /// </summary>
    private async Task OnSubmitAsync()
    {
        if (_isBusy) return;

        // Run both form validation and content validation together
        var formValid = _editContext.Validate();
        _contentHasError = string.IsNullOrWhiteSpace(_model.Message);

        if (!formValid || _contentHasError) return;

        _isBusy = true;
        _statusMessage = null;

        try
        {
            // Compose local DateTime from date/time pickers, then convert to UTC for the API
            DateTime? startsAtUtc = UserTimeZoneContext.ConvertToUtc(CombineDateAndTime(_startsAtDate, _startsAtTime));
            DateTime? expiresAtUtc = UserTimeZoneContext.ConvertToUtc(CombineDateAndTime(_expiresAtDate, _expiresAtTime));

            if (_isEditMode && ExistingAnnouncement is not null)
            {
                var request = new UpdateAnnouncementRequest
                {
                    Title = _model.Title,
                    Message = _model.Message,
                    DisplayType = _model.DisplayType,
                    Severity = _model.Severity,
                    StartsAtUtc = startsAtUtc,
                    ExpiresAtUtc = expiresAtUtc,
                    IsActive = _model.IsActive,
                    NotifyUsers = _model.NotifyUsers,
                    ClearDismissals = _clearDismissals
                };

                var result = await AnnouncementService.UpdateAsync(ExistingAnnouncement.Id, request);
                if (!result.Succeeded)
                {
                    _statusMessage = result.Error ?? "Failed to update announcement.";
                    return;
                }
            }
            else
            {
                var request = new CreateAnnouncementRequest
                {
                    Title = _model.Title,
                    Message = _model.Message,
                    DisplayType = _model.DisplayType,
                    Severity = _model.Severity,
                    StartsAtUtc = startsAtUtc,
                    ExpiresAtUtc = expiresAtUtc,
                    IsActive = _model.IsActive,
                    NotifyUsers = _model.NotifyUsers
                };

                var result = await AnnouncementService.CreateAsync(request);
                if (!result.Succeeded)
                {
                    _statusMessage = result.Error ?? "Failed to create announcement.";
                    return;
                }
            }

            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving announcement.");
            _statusMessage = "An unexpected error occurred. Please try again.";
        }
        finally
        {
            _isBusy = false;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Combines a date and time picker value into a single nullable DateTime (local time).
    /// Returns null if the date is not set.
    /// </summary>
    private static DateTime? CombineDateAndTime(DateTime? date, TimeSpan? time)
    {
        if (!date.HasValue) return null;
        return date.Value.Date + (time ?? TimeSpan.Zero);
    }

    #endregion

    #region Input Model

    /// <summary>
    /// Form model for the announcement create/edit dialog.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The plain-text title of the announcement.
        /// </summary>
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// The HTML content authored via Radzen HtmlEditor.
        /// Validation is handled manually since the editor doesn't integrate with DataAnnotationsValidator.
        /// </summary>
        [Required(ErrorMessage = "Content is required.")]
        [Display(Name = "Content")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The display type controlling where the announcement is surfaced.
        /// </summary>
        [Display(Name = "Display Type")]
        public AnnouncementDisplayType DisplayType { get; set; } = AnnouncementDisplayType.Standard;

        /// <summary>
        /// The severity level indicating announcement urgency.
        /// </summary>
        [Display(Name = "Severity")]
        public AnnouncementSeverity Severity { get; set; } = AnnouncementSeverity.Info;

        /// <summary>
        /// Whether the announcement is immediately active.
        /// </summary>
        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        /// <summary>
        /// Whether user notifications are sent when the announcement becomes active.
        /// </summary>
        [Display(Name = "Notify Users")]
        public bool NotifyUsers { get; set; } = true;
    }

    #endregion
}
