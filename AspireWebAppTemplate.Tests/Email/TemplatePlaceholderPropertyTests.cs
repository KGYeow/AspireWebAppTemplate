// Feature: email-smtp-integration, Property 3: Template placeholder replacement produces correct output
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
/// Property-based tests verifying that template placeholder replacement
/// produces correct output. For any set of placeholder key-value pairs, the rendered
/// output contains all values and no unresolved <c>{{Key}}</c> tokens remain.
/// </summary>
/// <remarks>
/// **Validates: Requirements 4.2, 4.3, 4.4, 8.2**
/// </remarks>
public class TemplatePlaceholderPropertyTests
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
    /// Seeds an active email template with placeholder tokens in both subject and body.
    /// The template uses <c>{{Key}}</c> placeholders that match the provided keys.
    /// </summary>
    private static void SeedTemplate(ApplicationDbContext dbContext, IReadOnlyList<string> placeholderKeys)
    {
        var subjectTemplate = "Hello {{" + string.Join("}} and {{", placeholderKeys) + "}}";
        var bodyTemplate = "<p>Welcome {{" + string.Join("}}</p><p>Info: {{", placeholderKeys) + "}}</p>";

        var template = new EmailTemplate
        {
            Id = Guid.NewGuid(),
            EmailType = EmailType.WelcomeEmail,
            DisplayName = "Welcome Email",
            Subject = subjectTemplate,
            HtmlBody = bodyTemplate,
            Category = EmailTemplateCategory.Business,
            IsActive = true,
            PlaceholderHints = string.Join(",", placeholderKeys),
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.EmailTemplates.Add(template);
        dbContext.SaveChanges();
    }

    /// <summary>
    /// Property: For any set of alphanumeric placeholder key-value pairs, rendering
    /// a template replaces ALL <c>{{Key}}</c> tokens with corresponding values
    /// and no unresolved <c>{{...}}</c> tokens remain in the output subject or body.
    /// **Validates: Requirements 4.2, 4.3, 4.4, 8.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property TemplatePlaceholderReplacement_ProducesCorrectOutput()
    {
        // Generator for placeholder keys: 1-5 unique alphanumeric keys (word characters only, matching \w+ regex).
        var keyGen = Gen.Elements("UserName", "CompanyName", "ResetLink", "DateCreated", "ActionUrl");
        var keyCountGen = Gen.Choose(1, 5);

        var keysGen = keyCountGen.SelectMany(count =>
            Gen.ArrayOf<string>(keyGen, count).Select(keys => keys.Distinct().ToList()));

        return Prop.ForAll(
            Arb.From(keysGen),
            (List<string> keys) =>
            {
                if (keys.Count == 0)
                    return true.Label("Skipped: no keys generated");

                var (dbContext, connection) = CreateDbContext();
                try
                {
                    // Seed an active template with the generated placeholder keys.
                    SeedTemplate(dbContext, keys);

                    // Create the service.
                    var logger = NullLogger<EmailTemplateService>.Instance;
                    var service = new EmailTemplateService(dbContext, logger);

                    // Build variables dictionary: map each key to a deterministic value.
                    var variables = new Dictionary<string, string>();
                    for (var i = 0; i < keys.Count; i++)
                    {
                        variables[keys[i]] = $"Value_{keys[i]}_{i}";
                    }

                    // Act: render the template.
                    var result = service.RenderAsync(EmailType.WelcomeEmail, variables)
                        .GetAwaiter().GetResult();

                    // Assert 1: All placeholder values appear in the rendered output.
                    var allValuesPresent = variables.Values.All(value =>
                        result.Subject.Contains(value) || result.HtmlBody.Contains(value));

                    // Assert 2: No unresolved {{Key}} tokens remain in subject or body.
                    var noUnresolvedInSubject = !result.Subject.Contains("{{");
                    var noUnresolvedInBody = !result.HtmlBody.Contains("{{");

                    var success = allValuesPresent && noUnresolvedInSubject && noUnresolvedInBody;

                    return success.Label(
                        $"AllValuesPresent={allValuesPresent}, " +
                        $"NoUnresolvedInSubject={noUnresolvedInSubject}, " +
                        $"NoUnresolvedInBody={noUnresolvedInBody}, " +
                        $"Subject='{result.Subject}', " +
                        $"Body='{result.HtmlBody}'");
                }
                finally
                {
                    dbContext.Dispose();
                    connection.Dispose();
                }
            });
    }
}
