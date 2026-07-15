using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Core.Contracts.Email;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.EmailTemplates;

/// <summary>
/// Edit dialog for business email templates. Pre-populates with the existing template values
/// and saves changes via <see cref="ApiEmailTemplateService.UpdateAsync"/>.
/// Uses Radzen HtmlEditor for the HTML body field. Validates that Subject and HtmlBody
/// are not empty before submitting.
/// </summary>
public partial class EditEmailTemplateDialog : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for email template operations.
    /// </summary>
    [Inject] private ApiEmailTemplateService EmailTemplateService { get; set; } = default!;

    /// <summary>
    /// Structured logger for diagnostics.
    /// </summary>
    [Inject] private ILogger<EditEmailTemplateDialog> Logger { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// The MudBlazor dialog instance for closing/canceling the dialog.
    /// </summary>
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    #endregion

    #region Parameters

    /// <summary>
    /// The email template to edit. Passed from the parent page when opening the dialog.
    /// </summary>
    [Parameter]
    public EmailTemplateDto? Template { get; set; }

    #endregion

    #region State

    /// <summary>
    /// The form input model populated from the existing template.
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
    /// Whether the HTML body field is in an error state (empty on submit attempt).
    /// </summary>
    private bool _htmlBodyHasError;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the form model with values from the existing template.
    /// </summary>
    protected override void OnInitialized()
    {
        if (Template is not null)
        {
            _model = new InputModel
            {
                DisplayName = Template.DisplayName,
                Subject = Template.Subject,
                HtmlBody = Template.HtmlBody,
                IsActive = Template.IsActive
            };
        }

        _editContext = new EditContext(_model);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Validates and submits the form to update the business email template.
    /// Rejects empty Subject or HtmlBody. On success, closes the dialog with a positive result.
    /// </summary>
    private async Task OnSubmitAsync()
    {
        if (_isBusy) return;

        // Run both form validation and HTML body validation together
        var formValid = _editContext.Validate();
        _htmlBodyHasError = string.IsNullOrWhiteSpace(_model.HtmlBody);

        if (!formValid || _htmlBodyHasError) return;

        _isBusy = true;
        _statusMessage = null;

        try
        {
            var request = new UpdateEmailTemplateRequest
            {
                DisplayName = _model.DisplayName,
                Subject = _model.Subject,
                HtmlBody = _model.HtmlBody,
                PlaceholderHints = Template!.PlaceholderHints, // Preserved as-is (read-only, set by seed data)
                IsActive = _model.IsActive
            };

            var result = await EmailTemplateService.UpdateAsync(Template!.Id, request);

            if (!result.Succeeded)
            {
                _statusMessage = result.Error ?? "Failed to update email template.";
                return;
            }

            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating email template.");
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
    /// Formats comma-separated placeholder hints into a readable display format
    /// showing each placeholder wrapped in {{braces}}.
    /// </summary>
    /// <param name="hints">Comma-separated placeholder names.</param>
    /// <returns>A formatted string like "{{UserName}}, {{ResetLink}}".</returns>
    private static string FormatPlaceholderHints(string hints)
    {
        if (string.IsNullOrWhiteSpace(hints)) return string.Empty;

        var placeholders = hints.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(", ", placeholders.Select(p => $"{{{{{p}}}}}"));
    }

    #endregion

    #region Input Model

    /// <summary>
    /// Form model for the email template edit dialog.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The human-readable display name of the template.
        /// </summary>
        [Required(ErrorMessage = "Display name is required.")]
        [StringLength(200, ErrorMessage = "Display name cannot exceed 200 characters.")]
        [Display(Name = "Display Name")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// The email subject line template. Supports {{placeholder}} syntax.
        /// </summary>
        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(500, ErrorMessage = "Subject cannot exceed 500 characters.")]
        [Display(Name = "Subject")]
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// The HTML body template content. Validated manually since RadzenHtmlEditor
        /// does not integrate with DataAnnotationsValidator.
        /// </summary>
        public string HtmlBody { get; set; } = string.Empty;

        /// <summary>
        /// Whether the template is active and available for sending.
        /// </summary>
        [Display(Name = "Active")]
        public bool IsActive { get; set; }
    }

    #endregion
}
