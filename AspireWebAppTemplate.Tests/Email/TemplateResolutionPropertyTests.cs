// Feature: email-smtp-integration, Property 4: Inactive or missing EmailType template is rejected
// Feature: email-smtp-integration, Property 5: Template resolution uses unified database query
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Domain.Entities.Template;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Infrastructure.Services.Template.Email;
using AspireWebAppTemplate.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AspireWebAppTemplate.Tests.Email;

/// <summary>
/// Property-based tests verifying that the <see cref="EmailTemplateService"/> correctly
/// rejects requests for inactive or missing <see cref="EmailType"/> templates
/// by throwing <see cref="KeyNotFoundException"/>, and resolves all templates from
/// the database using the unified rendering pipeline.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Validates: Requirements 4.6</strong> — IF a template for the requested EmailType
/// does not exist or is inactive, THEN the Template_Service SHALL throw a
/// KeyNotFoundException indicating the template was not found or is disabled.
/// </para>
/// <para>
/// <strong>Validates: Requirements 3.6, 8.1, 8.2</strong> — All templates are resolved from
/// the database using the same {{placeholder}} rendering pipeline.
/// </para>
/// <para>
/// Uses SQLite in-memory database to test the real <see cref="EmailTemplateService"/>
/// query logic against the database layer.
/// </para>
/// </remarks>
public class TemplateResolutionPropertyTests
{
    /// <summary>
    /// Creates a SQLite in-memory <see cref="ApplicationDbContext"/> for testing.
    /// Foreign key enforcement is disabled to allow minimal test setup.
    /// </summary>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = OFF;";
        cmd.ExecuteNonQuery();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();
        return (dbContext, connection);
    }

    /// <summary>
    /// Creates an <see cref="EmailTemplateService"/> instance for testing.
    /// </summary>
    private static EmailTemplateService CreateService(ApplicationDbContext dbContext)
    {
        var logger = NullLogger<EmailTemplateService>.Instance;
        return new EmailTemplateService(dbContext, logger);
    }

    /// <summary>
    /// Property: For ANY <see cref="EmailType"/> value, when the corresponding template
    /// exists in the database but has <c>IsActive = false</c>, calling
    /// <see cref="EmailTemplateService.RenderAsync"/> SHALL throw
    /// <see cref="KeyNotFoundException"/>.
    /// **Validates: Requirements 4.6**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property InactiveTemplate_ThrowsKeyNotFoundException()
    {
        var emailTypeGen = Gen.Elements(Enum.GetValues<EmailType>());

        return Prop.ForAll(
            Arb.From(emailTypeGen),
            (EmailType emailType) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // Seed an inactive template for the given EmailType.
                    var template = new EmailTemplate
                    {
                        Id = Guid.NewGuid(),
                        EmailType = emailType,
                        DisplayName = $"Test {emailType}",
                        Subject = "Test Subject {{UserName}}",
                        HtmlBody = "<p>Hello {{UserName}}</p>",
                        Category = EmailTemplateCategory.Business,
                        IsActive = false,
                        PlaceholderHints = "UserName",
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    dbContext.EmailTemplates.Add(template);
                    dbContext.SaveChanges();

                    var service = CreateService(dbContext);
                    var variables = new Dictionary<string, string> { { "UserName", "TestUser" } };

                    // Act & Assert: calling RenderAsync should throw KeyNotFoundException.
                    var threwExpectedException = false;
                    try
                    {
                        service.RenderAsync(emailType, variables)
                            .GetAwaiter().GetResult();
                    }
                    catch (KeyNotFoundException)
                    {
                        threwExpectedException = true;
                    }

                    return threwExpectedException.Label(
                        $"Expected KeyNotFoundException for inactive template of type '{emailType}', but no exception was thrown.");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }

    /// <summary>
    /// Property: For ANY <see cref="EmailType"/> value, when NO template exists in the
    /// database for that type, calling <see cref="EmailTemplateService.RenderAsync"/>
    /// SHALL throw <see cref="KeyNotFoundException"/>.
    /// **Validates: Requirements 4.6**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property MissingTemplate_ThrowsKeyNotFoundException()
    {
        var emailTypeGen = Gen.Elements(Enum.GetValues<EmailType>());

        return Prop.ForAll(
            Arb.From(emailTypeGen),
            (EmailType emailType) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // Do NOT seed any template — the database is empty for this EmailType.
                    var service = CreateService(dbContext);
                    var variables = new Dictionary<string, string> { { "UserName", "TestUser" } };

                    // Act & Assert: calling RenderAsync should throw KeyNotFoundException.
                    var threwExpectedException = false;
                    try
                    {
                        service.RenderAsync(emailType, variables)
                            .GetAwaiter().GetResult();
                    }
                    catch (KeyNotFoundException)
                    {
                        threwExpectedException = true;
                    }

                    return threwExpectedException.Label(
                        $"Expected KeyNotFoundException for missing template of type '{emailType}', but no exception was thrown.");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }

    /// <summary>
    /// Property: For ANY template in the database, calling <see cref="EmailTemplateService.RenderPreviewAsync"/>
    /// SHALL resolve content from the database and replace <c>{{Key}}</c> placeholders with the
    /// provided sample data values — regardless of whether it is a system or business template.
    /// **Validates: Requirements 3.6, 8.1, 8.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property AllTemplates_ResolveFromDatabase()
    {
        // Generate a simple alphanumeric key to use as a placeholder name.
        var keyGen = Gen.Elements("UserName", "ResetLink", "Code", "AppName");
        var valueGen = Gen.Elements("Alice", "Bob", "Charlie", "https://example.com", "123456");
        var categoryGen = Gen.Elements(EmailTemplateCategory.System, EmailTemplateCategory.Business);

        return Prop.ForAll(
            Arb.From(keyGen),
            Arb.From(valueGen),
            Arb.From(categoryGen),
            (string key, string value, EmailTemplateCategory category) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // Arrange: seed a template with {{Key}} placeholders.
                    var templateId = Guid.NewGuid();
                    var emailType = category == EmailTemplateCategory.System
                        ? EmailType.PasswordReset
                        : EmailType.CustomNotification;

                    var template = new EmailTemplate
                    {
                        Id = templateId,
                        EmailType = emailType,
                        DisplayName = "Test Template",
                        Subject = $"Subject for {{{{{key}}}}}",
                        HtmlBody = $"<p>Dear {{{{{key}}}}}, this is your notification.</p>",
                        Category = category,
                        IsActive = true,
                        PlaceholderHints = key,
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    dbContext.EmailTemplates.Add(template);
                    dbContext.SaveChanges();

                    // Act: call RenderPreviewAsync which resolves from database for all templates.
                    var service = CreateService(dbContext);
                    var sampleData = new Dictionary<string, string> { { key, value } };
                    var result = service.RenderPreviewAsync(templateId, sampleData)
                        .GetAwaiter().GetResult();

                    // Assert: placeholders should be replaced with the sample data value.
                    var subjectContainsValue = result.Subject.Contains(value);
                    var bodyContainsValue = result.HtmlBody.Contains(value);
                    var subjectNoPlaceholder = !result.Subject.Contains($"{{{{{key}}}}}");
                    var bodyNoPlaceholder = !result.HtmlBody.Contains($"{{{{{key}}}}}");

                    return (subjectContainsValue && bodyContainsValue && subjectNoPlaceholder && bodyNoPlaceholder)
                        .Label($"Template (category={category}) should resolve from database. " +
                               $"Subject has value: {subjectContainsValue}, Body has value: {bodyContainsValue}, " +
                               $"Subject no placeholder: {subjectNoPlaceholder}, Body no placeholder: {bodyNoPlaceholder}. " +
                               $"Actual subject: '{result.Subject}', Actual body: '{result.HtmlBody}'");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
