using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.Template.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspireWebAppTemplate.ApiService.Controllers.Template;

/// <summary>
/// Provides email template query, edit, and preview endpoints.
/// This controller is intentionally thin — it handles HTTP concerns only (request parsing,
/// user identity extraction, status code mapping) and delegates all business logic to
/// <see cref="IEmailTemplateService"/>.
/// </summary>
/// <remarks>
/// <para>
/// The template set is fixed by the <see cref="Domain.Enums.EmailType"/> enum and seed data.
/// This controller does NOT expose POST (create) or DELETE endpoints — administrators
/// can only edit existing business templates.
/// </para>
/// <para>
/// Exception-to-HTTP-status mapping:
/// <list type="bullet">
///   <item><see cref="KeyNotFoundException"/> → 404 Not Found</item>
///   <item><see cref="InvalidOperationException"/> → 400 Bad Request</item>
///   <item><see cref="ArgumentException"/> → 400 Bad Request</item>
/// </list>
/// </para>
/// </remarks>
[Route("api/email-templates")]
[Authorize]
public class EmailTemplateController : BaseController
{
    #region Constructor

    /// <summary>
    /// The email template service for template query, edit, and preview operations.
    /// </summary>
    private readonly IEmailTemplateService _templateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailTemplateController"/> class.
    /// </summary>
    /// <param name="templateService">The email template service for query, edit, and preview operations.</param>
    public EmailTemplateController(IEmailTemplateService templateService)
    {
        _templateService = templateService;
    }

    #endregion

    #region Query Operations

    /// <summary>
    /// Retrieves all email templates (system metadata and business templates).
    /// </summary>
    /// <returns>A list of all email templates.</returns>
    /// <response code="200">Returns the list of all email templates.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<EmailTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _templateService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single email template by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the email template.</param>
    /// <returns>The email template matching the specified ID.</returns>
    /// <response code="200">Returns the requested email template.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">No template exists with the specified ID.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmailTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var result = await _templateService.GetByIdAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    #endregion

    #region Edit Operations

    /// <summary>
    /// Updates an existing business notification template.
    /// Rejects updates to system templates with a 400 Bad Request.
    /// </summary>
    /// <param name="id">The unique identifier of the template to update.</param>
    /// <param name="request">The update request containing new field values.</param>
    /// <returns>The updated email template.</returns>
    /// <response code="200">The template was updated successfully.</response>
    /// <response code="400">The update targets a system template or the request is invalid.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">No template exists with the specified ID.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmailTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmailTemplateRequest request)
    {
        try
        {
            var result = await _templateService.UpdateAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    #endregion

    #region Preview Operations

    /// <summary>
    /// Renders a template with sample data for admin preview.
    /// Returns the rendered subject and HTML body.
    /// </summary>
    /// <param name="id">The unique identifier of the template to preview.</param>
    /// <param name="request">The preview request containing sample placeholder values.</param>
    /// <returns>The rendered email result with subject and HTML body.</returns>
    /// <response code="200">Returns the rendered preview result.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">No template exists with the specified ID.</response>
    [HttpPost("{id:guid}/preview")]
    [ProducesResponseType(typeof(RenderedEmailResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview(Guid id, [FromBody] PreviewTemplateRequest request)
    {
        try
        {
            var result = await _templateService.RenderPreviewAsync(id, request.SampleData);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    #endregion
}
