using System.Net.Http.Json;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts.Email;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// Typed HttpClient service for email template management API operations.
/// Uses Aspire service discovery and <see cref="UserIdentityDelegatingHandler"/> for auth propagation.
/// Supports read, edit, and preview operations — template creation and deletion are not available.
/// </summary>
public class ApiEmailTemplateService
{
    #region Constructor

    /// <summary>
    /// The underlying HttpClient configured with the ApiService base address.
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of <see cref="ApiEmailTemplateService"/> with the configured HttpClient.
    /// </summary>
    /// <param name="http">The HttpClient instance configured via Aspire service discovery.</param>
    public ApiEmailTemplateService(HttpClient http)
    {
        _http = http;
    }

    #endregion

    #region Query Operations

    /// <summary>
    /// Retrieves all email templates (both system metadata and business templates).
    /// Calls GET /api/email-templates.
    /// </summary>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the list of all email templates on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<List<EmailTemplateDto>>> GetAllAsync()
    {
        var response = await _http.GetAsync("/api/email-templates");
        if (response.IsSuccessStatusCode)
            return ApiResult<List<EmailTemplateDto>>.Success(await response.Content.ReadFromJsonAsync<List<EmailTemplateDto>>() ?? []);
        return ApiResult<List<EmailTemplateDto>>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Retrieves a single email template by its unique identifier.
    /// Calls GET /api/email-templates/{id}.
    /// </summary>
    /// <param name="id">The unique identifier of the email template.</param>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the email template on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<EmailTemplateDto>> GetByIdAsync(Guid id)
    {
        var response = await _http.GetAsync($"/api/email-templates/{id}");
        if (response.IsSuccessStatusCode)
            return ApiResult<EmailTemplateDto>.Success(await response.Content.ReadFromJsonAsync<EmailTemplateDto>()!);
        return ApiResult<EmailTemplateDto>.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Edit Operations

    /// <summary>
    /// Updates an existing business notification template's content and settings.
    /// Calls PUT /api/email-templates/{id}.
    /// </summary>
    /// <param name="id">The unique identifier of the template to update.</param>
    /// <param name="request">The update request containing modified fields.</param>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the updated email template on success,
    /// or an error message on failure (e.g., attempting to update a system template).
    /// </returns>
    public async Task<ApiResult<EmailTemplateDto>> UpdateAsync(Guid id, UpdateEmailTemplateRequest request)
    {
        var response = await _http.PutAsJsonAsync($"/api/email-templates/{id}", request);
        if (response.IsSuccessStatusCode)
            return ApiResult<EmailTemplateDto>.Success(await response.Content.ReadFromJsonAsync<EmailTemplateDto>()!);
        return ApiResult<EmailTemplateDto>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Renders a template with sample data for admin preview purposes.
    /// Calls POST /api/email-templates/{id}/preview.
    /// </summary>
    /// <param name="id">The unique identifier of the template to preview.</param>
    /// <param name="request">The preview request containing sample placeholder values.</param>
    /// <returns>
    /// An <see cref="ApiResult{T}"/> containing the rendered email result on success,
    /// or an error message on failure.
    /// </returns>
    public async Task<ApiResult<RenderedEmailResult>> PreviewAsync(Guid id, PreviewTemplateRequest request)
    {
        var response = await _http.PostAsJsonAsync($"/api/email-templates/{id}/preview", request);
        if (response.IsSuccessStatusCode)
            return ApiResult<RenderedEmailResult>.Success(await response.Content.ReadFromJsonAsync<RenderedEmailResult>()!);
        return ApiResult<RenderedEmailResult>.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion
}
