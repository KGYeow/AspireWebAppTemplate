using AspireWebAppTemplate.Application.Features.Template.Email;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.EmailTemplates;

/// <summary>
/// Dialog component that renders an email template preview with sample data.
/// Accepts a template via parameter, generates sample placeholder values from
/// <see cref="EmailTemplateDto.PlaceholderHints"/>, calls the preview API,
/// and displays the rendered subject and HTML body.
/// </summary>
public partial class PreviewEmailTemplateDialog : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for email template operations.
    /// </summary>
    [Inject] private ApiEmailTemplateService EmailTemplateService { get; set; } = default!;

    /// <summary>
    /// Structured logger.
    /// </summary>
    [Inject] private ILogger<PreviewEmailTemplateDialog> Logger { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// The MudBlazor dialog instance for controlling close behavior.
    /// </summary>
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    #endregion

    #region Parameters

    /// <summary>
    /// The email template to preview. Used to extract the template ID and placeholder hints.
    /// </summary>
    [Parameter]
    public EmailTemplateDto Template { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// Whether the preview is currently loading from the API.
    /// </summary>
    private bool _isLoading = true;

    /// <summary>
    /// The rendered email subject line after preview rendering.
    /// </summary>
    private string _renderedSubject = string.Empty;

    /// <summary>
    /// The rendered HTML body after preview rendering.
    /// </summary>
    private string _renderedHtmlBody = string.Empty;

    /// <summary>
    /// Error message displayed when the preview API call fails.
    /// </summary>
    private string? _errorMessage;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads the preview on component initialization by calling the preview API
    /// with sample data generated from the template's placeholder hints.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await LoadPreviewAsync();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Calls the preview API with sample placeholder data and populates the rendered result.
    /// </summary>
    private async Task LoadPreviewAsync()
    {
        _isLoading = true;
        _errorMessage = null;

        try
        {
            var sampleData = GenerateSampleData(Template.PlaceholderHints);
            var request = new PreviewTemplateRequest { SampleData = sampleData };
            var result = await EmailTemplateService.PreviewAsync(Template.Id, request);

            if (result.Succeeded && result.Data is not null)
            {
                _renderedSubject = result.Data.Subject;
                _renderedHtmlBody = result.Data.HtmlBody;
            }
            else
            {
                _errorMessage = result.Error ?? "Failed to load template preview.";
                Logger.LogError("Failed to preview template {TemplateId}: {Error}", Template.Id, result.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error loading template preview for {TemplateId}.", Template.Id);
            _errorMessage = "An unexpected error occurred while loading the preview.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Generates sample placeholder values from the comma-separated placeholder hints string.
    /// Each hint is converted to a sample value like "Sample {HintName}".
    /// </summary>
    /// <param name="placeholderHints">Comma-separated placeholder variable names.</param>
    /// <returns>A dictionary of placeholder names to sample values.</returns>
    private static Dictionary<string, string> GenerateSampleData(string placeholderHints)
    {
        var sampleData = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(placeholderHints))
            return sampleData;

        var hints = placeholderHints.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var hint in hints)
        {
            sampleData[hint] = GenerateSampleValue(hint);
        }

        return sampleData;
    }

    /// <summary>
    /// Generates a contextual sample value based on the placeholder name.
    /// Recognizes common placeholder names and provides realistic sample values.
    /// </summary>
    /// <param name="placeholder">The placeholder variable name.</param>
    /// <returns>A sample value appropriate for the placeholder.</returns>
    private static string GenerateSampleValue(string placeholder)
    {
        return placeholder.ToLowerInvariant() switch
        {
            "username" => "John Doe",
            "resetlink" => "https://example.com/reset?token=sample-token",
            "confirmationlink" => "https://example.com/confirm?token=sample-token",
            "twofactorcode" => "123456",
            "lockoutend" => DateTime.UtcNow.AddMinutes(15).ToString("g"),
            "newemail" => "newemail@example.com",
            "deactivationreason" => "Account deactivated by administrator.",
            "subject" => "Sample Email Subject",
            "body" => "This is a sample email body content for preview purposes.",
            _ => $"Sample {placeholder}"
        };
    }

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
}
