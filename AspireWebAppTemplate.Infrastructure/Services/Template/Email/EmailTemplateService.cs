using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Domain.Entities.Template;
using AspireWebAppTemplate.Application.Features.Template.Email;
using AspireWebAppTemplate.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.Infrastructure.Services.Template.Email;

/// <summary>
/// Implements the <see cref="IEmailTemplateService"/> interface to resolve, render, and manage
/// email templates from the database. All templates (system and business) use the same
/// <c>{{placeholder}}</c> string replacement rendering pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Templates are queried by <see cref="EmailType"/> from the database. Rendering replaces
/// <c>{{Key}}</c> placeholders in both subject and body with provided variable values.
/// </para>
/// <para>
/// <strong>Edit operations:</strong> Only business templates can be updated. Attempts to update
/// system templates are rejected with <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// Registered as a scoped service to align with the per-request <see cref="ApplicationDbContext"/> lifetime.
/// </para>
/// </remarks>
public partial class EmailTemplateService : IEmailTemplateService
{
    #region Constructor

    /// <summary>
    /// The application database context for querying and persisting email template data.
    /// </summary>
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// The logger instance for recording template resolution errors and warnings.
    /// </summary>
    private readonly ILogger<EmailTemplateService> _logger;

    /// <summary>
    /// Compiled regex pattern for matching <c>{{Key}}</c> placeholders in template content.
    /// Captures the key name within the double curly braces for replacement.
    /// </summary>
    [GeneratedRegex(@"\{\{(\w+)\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailTemplateService"/> class.
    /// </summary>
    /// <param name="dbContext">The application database context for querying and persisting email template data.</param>
    /// <param name="logger">The logger instance for recording template resolution errors and warnings.</param>
    public EmailTemplateService(
        ApplicationDbContext dbContext,
        ILogger<EmailTemplateService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    #endregion

    #region Template Rendering

    /// <inheritdoc />
    public async Task<RenderedEmailResult> RenderAsync(EmailType emailType, Dictionary<string, string> variables)
    {
        var template = await _dbContext.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.EmailType == emailType && t.IsActive);

        if (template is null)
        {
            throw new KeyNotFoundException(
                $"No active email template found for type '{emailType}'.");
        }

        // Replace {{Key}} placeholders in both subject and body.
        var renderedSubject = RenderPlaceholders(template.Subject, variables);
        var renderedBody = RenderPlaceholders(template.HtmlBody, variables);

        return new RenderedEmailResult
        {
            Subject = renderedSubject,
            HtmlBody = renderedBody
        };
    }

    /// <inheritdoc />
    public async Task<RenderedEmailResult> RenderPreviewAsync(Guid templateId, Dictionary<string, string> sampleData)
    {
        var template = await _dbContext.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId);

        if (template is null)
        {
            throw new KeyNotFoundException(
                $"Email template with ID '{templateId}' was not found.");
        }

        // Render the database content with sample data using {{placeholder}} replacement.
        var renderedSubject = RenderPlaceholders(template.Subject, sampleData);
        var renderedBody = RenderPlaceholders(template.HtmlBody, sampleData);

        return new RenderedEmailResult
        {
            Subject = renderedSubject,
            HtmlBody = renderedBody
        };
    }

    #endregion

    #region Query Operations

    /// <inheritdoc />
    public async Task<List<EmailTemplateDto>> GetAllAsync()
    {
        var templates = await _dbContext.EmailTemplates
            .AsNoTracking()
            .OrderBy(t => t.Category)
            .ThenBy(t => t.DisplayName)
            .ToListAsync();

        return templates.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<EmailTemplateDto> GetByIdAsync(Guid id)
    {
        var template = await _dbContext.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (template is null)
        {
            throw new KeyNotFoundException(
                $"Email template with ID '{id}' was not found.");
        }

        return MapToDto(template);
    }

    #endregion

    #region Edit Operations

    /// <inheritdoc />
    public async Task<EmailTemplateDto> UpdateAsync(Guid id, UpdateEmailTemplateRequest request)
    {
        var template = await _dbContext.EmailTemplates
            .FirstOrDefaultAsync(t => t.Id == id);

        if (template is null)
        {
            throw new KeyNotFoundException(
                $"Email template with ID '{id}' was not found.");
        }

        // Reject updates to system security templates — they are read-only at runtime.
        if (template.Category == EmailTemplateCategory.System)
        {
            throw new InvalidOperationException(
                "System security templates cannot be modified via the API. They are read-only at runtime and require code deployment to change.");
        }

        // Apply updates to the business template fields.
        template.DisplayName = request.DisplayName;
        template.Subject = request.Subject;
        template.HtmlBody = request.HtmlBody;
        template.PlaceholderHints = request.PlaceholderHints;
        template.IsActive = request.IsActive;
        template.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return MapToDto(template);
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Replaces all <c>{{Key}}</c> placeholders in template content with the
    /// corresponding values from the provided variables dictionary. Unmatched placeholders
    /// are replaced with an empty string.
    /// </summary>
    /// <param name="content">The template content with <c>{{Key}}</c> placeholders.</param>
    /// <param name="variables">Dictionary of placeholder names to replacement values.</param>
    /// <returns>The rendered content with all placeholders replaced.</returns>
    private static string RenderPlaceholders(string content, Dictionary<string, string> variables)
    {
        return PlaceholderRegex().Replace(content, match =>
        {
            var key = match.Groups[1].Value;
            return variables.TryGetValue(key, out var value) ? value : string.Empty;
        });
    }

    /// <summary>
    /// Maps an <see cref="EmailTemplate"/> entity to an <see cref="EmailTemplateDto"/> response object.
    /// </summary>
    /// <param name="entity">The email template entity to map.</param>
    /// <returns>An <see cref="EmailTemplateDto"/> with all fields populated from the entity.</returns>
    private static EmailTemplateDto MapToDto(EmailTemplate entity)
    {
        return new EmailTemplateDto
        {
            Id = entity.Id,
            EmailType = entity.EmailType,
            DisplayName = entity.DisplayName,
            Subject = entity.Subject,
            HtmlBody = entity.HtmlBody,
            Category = entity.Category,
            IsActive = entity.IsActive,
            PlaceholderHints = entity.PlaceholderHints,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    #endregion
}
