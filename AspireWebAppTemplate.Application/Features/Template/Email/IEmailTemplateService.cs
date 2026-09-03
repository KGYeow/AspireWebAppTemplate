using AspireWebAppTemplate.Application.Features.Template.Email;
using AspireWebAppTemplate.Domain.Enums;

namespace AspireWebAppTemplate.Application.Features.Template.Email;

/// <summary>
/// Defines the contract for email template resolution, rendering, and management.
/// All templates (system and business) are stored in and resolved from the database.
/// The <see cref="EmailTemplateCategory"/> determines editability — not storage location.
/// </summary>
/// <remarks>
/// <para>
/// System security templates are read-only at runtime. Business notification templates
/// use an edit-only model: each business <see cref="EmailType"/> has exactly one template
/// in the database (seeded on first deployment). Administrators can edit business template
/// content but cannot create new templates or delete existing ones.
/// </para>
/// <para>
/// Registered as a scoped service to align with per-request DbContext lifetime.
/// </para>
/// </remarks>
public interface IEmailTemplateService
{
    #region Template Rendering

    /// <summary>
    /// Renders the template for the specified <see cref="EmailType"/> from the database
    /// with the provided variables. Uses <c>{{placeholder}}</c> string replacement on
    /// both subject and body.
    /// </summary>
    /// <param name="emailType">The email type to resolve and render.</param>
    /// <param name="variables">Dictionary of placeholder names to values.</param>
    /// <returns>A <see cref="RenderedEmailResult"/> containing the rendered subject and HTML body.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no active template exists for the type.</exception>
    Task<RenderedEmailResult> RenderAsync(EmailType emailType, Dictionary<string, string> variables);

    /// <summary>
    /// Renders any template with sample data for admin preview purposes.
    /// </summary>
    /// <param name="templateId">The unique identifier of the template to preview.</param>
    /// <param name="sampleData">Dictionary of sample placeholder values.</param>
    /// <returns>A <see cref="RenderedEmailResult"/> containing the preview-rendered subject and HTML body.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the template does not exist.</exception>
    Task<RenderedEmailResult> RenderPreviewAsync(Guid templateId, Dictionary<string, string> sampleData);

    #endregion

    #region Query Operations

    /// <summary>
    /// Retrieves all email templates (both system and business) from the database.
    /// </summary>
    /// <returns>A list of all <see cref="EmailTemplateDto"/> records.</returns>
    Task<List<EmailTemplateDto>> GetAllAsync();

    /// <summary>
    /// Retrieves a single email template by its unique identifier.
    /// </summary>
    /// <param name="id">The template's unique identifier.</param>
    /// <returns>The <see cref="EmailTemplateDto"/> for the specified template.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no template exists with the specified ID.</exception>
    Task<EmailTemplateDto> GetByIdAsync(Guid id);

    #endregion

    #region Edit Operations

    /// <summary>
    /// Updates an existing business notification template. Rejects updates to system templates.
    /// This is the only mutation operation — no create or delete is supported.
    /// </summary>
    /// <param name="id">The template's unique identifier.</param>
    /// <param name="request">The <see cref="UpdateEmailTemplateRequest"/> with updated fields.</param>
    /// <returns>The updated <see cref="EmailTemplateDto"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no template exists with the specified ID.</exception>
    /// <exception cref="InvalidOperationException">Thrown when attempting to update a system template.</exception>
    Task<EmailTemplateDto> UpdateAsync(Guid id, UpdateEmailTemplateRequest request);

    #endregion
}
