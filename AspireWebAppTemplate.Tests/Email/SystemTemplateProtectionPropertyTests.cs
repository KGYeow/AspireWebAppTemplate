// Feature: email-smtp-integration, Property 6: System templates cannot be updated
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Domain.Entities;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Application.Features.Template.Email;
using AspireWebAppTemplate.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AspireWebAppTemplate.Tests.Email;

/// <summary>
/// Property-based tests verifying that system templates cannot be updated via the
/// <see cref="EmailTemplateService.UpdateAsync"/> method. For any valid
/// <see cref="UpdateEmailTemplateRequest"/>, calling UpdateAsync on a system template
/// throws <see cref="InvalidOperationException"/> and the database record remains unchanged.
/// </summary>
/// <remarks>
/// **Validates: Requirements 5.3, 5.6**
/// </remarks>
public class SystemTemplateProtectionPropertyTests
{
    /// <summary>
    /// Creates a SQLite in-memory ApplicationDbContext for testing.
    /// Foreign key enforcement is disabled to allow seeding minimal entities
    /// without satisfying all relational constraints.
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
    /// Seeds a system security template in the database with known fixed values.
    /// </summary>
    private static void SeedSystemTemplate(ApplicationDbContext dbContext, Guid templateId)
    {
        var template = new EmailTemplate
        {
            Id = templateId,
            EmailType = EmailType.PasswordReset,
            DisplayName = "Password Reset",
            Subject = "Reset Your Password",
            HtmlBody = "<p>Original system template body</p>",
            Category = EmailTemplateCategory.System,
            IsActive = true,
            PlaceholderHints = "UserName,ResetLink",
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.EmailTemplates.Add(template);
        dbContext.SaveChanges();
    }

    /// <summary>
    /// Property: For ANY valid <see cref="UpdateEmailTemplateRequest"/>, calling
    /// <see cref="EmailTemplateService.UpdateAsync"/> on a system template (Category = System)
    /// SHALL throw <see cref="InvalidOperationException"/> and the template record in the
    /// database SHALL remain unchanged.
    /// **Validates: Requirements 5.3, 5.6**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property SystemTemplates_CannotBeUpdated_ThrowsInvalidOperationException()
    {
        // Generator for valid DisplayName values (required, max 200 chars).
        var displayNameGen = Gen.Elements(
            "Updated Template", "New Display Name", "Modified Name", "Test Template");

        // Generator for valid Subject values (required, max 500 chars).
        var subjectGen = Gen.Elements(
            "New Subject Line", "Updated Subject {{UserName}}", "Test Subject", "Changed Subject");

        // Generator for valid HtmlBody values (required).
        var htmlBodyGen = Gen.Elements(
            "<p>Updated body</p>", "<h1>New Content</h1>", "<div>Changed HTML</div>", "<p>{{UserName}} welcome</p>");

        // Generator for PlaceholderHints values (optional, max 1000 chars).
        var placeholderHintsGen = Gen.Elements(
            "UserName,ResetLink", "UserName", "UserName,CompanyName,ActionUrl", "");

        // Generator for IsActive boolean.
        var isActiveGen = Gen.Elements(true, false);

        // Combine all generators into an UpdateEmailTemplateRequest.
        var requestGen = displayNameGen.SelectMany(displayName =>
            subjectGen.SelectMany(subject =>
                htmlBodyGen.SelectMany(htmlBody =>
                    placeholderHintsGen.SelectMany(hints =>
                        isActiveGen.Select(isActive => new UpdateEmailTemplateRequest
                        {
                            DisplayName = displayName,
                            Subject = subject,
                            HtmlBody = htmlBody,
                            PlaceholderHints = hints,
                            IsActive = isActive
                        })))));

        return Prop.ForAll(
            Arb.From(requestGen),
            (UpdateEmailTemplateRequest request) =>
            {
                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // Use a consistent template ID per test run.
                    var templateId = Guid.NewGuid();

                    // Seed a system template with known original values.
                    SeedSystemTemplate(dbContext, templateId);

                    // Create the service.
                    var logger = NullLogger<EmailTemplateService>.Instance;
                    var service = new EmailTemplateService(dbContext, logger);

                    // Act: Attempt to update the system template — should throw.
                    var threwException = false;
                    try
                    {
                        service.UpdateAsync(templateId, request).GetAwaiter().GetResult();
                    }
                    catch (InvalidOperationException)
                    {
                        threwException = true;
                    }

                    // Assert 1: InvalidOperationException was thrown.
                    if (!threwException)
                    {
                        return false.Label("Expected InvalidOperationException was not thrown");
                    }

                    // Assert 2: The database record remains unchanged.
                    var entity = dbContext.EmailTemplates
                        .AsNoTracking()
                        .First(t => t.Id == templateId);

                    var displayNameUnchanged = entity.DisplayName == "Password Reset";
                    var subjectUnchanged = entity.Subject == "Reset Your Password";
                    var bodyUnchanged = entity.HtmlBody == "<p>Original system template body</p>";
                    var hintsUnchanged = entity.PlaceholderHints == "UserName,ResetLink";
                    var isActiveUnchanged = entity.IsActive;
                    var updatedAtNull = entity.UpdatedAtUtc == null;

                    var recordUnchanged = displayNameUnchanged && subjectUnchanged &&
                                          bodyUnchanged && hintsUnchanged &&
                                          isActiveUnchanged && updatedAtNull;

                    return recordUnchanged.Label(
                        $"RecordUnchanged={recordUnchanged}, " +
                        $"DisplayName={displayNameUnchanged}, Subject={subjectUnchanged}, " +
                        $"Body={bodyUnchanged}, Hints={hintsUnchanged}, " +
                        $"IsActive={isActiveUnchanged}, UpdatedAt={updatedAtNull}");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
