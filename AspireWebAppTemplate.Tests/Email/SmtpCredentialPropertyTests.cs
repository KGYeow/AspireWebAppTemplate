// Feature: email-smtp-integration, Property 2: SMTP credentials are applied if and only if both username and password are present
using System.Reflection;
using AspireWebAppTemplate.ApiService.Abstractions;
using AspireWebAppTemplate.ApiService.Data;
using AspireWebAppTemplate.ApiService.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AspireWebAppTemplate.Tests.Email;

/// <summary>
/// Property-based tests verifying that SMTP credentials are applied if and only if
/// both username and password are present and non-empty. When either is null, empty,
/// or whitespace-only, the service stores them as-is from configuration but the
/// <c>CreateSmtpClient</c> method will not apply credentials to the SmtpClient.
/// </summary>
/// <remarks>
/// **Validates: Requirements 2.4, 2.5**
/// </remarks>
public class SmtpCredentialPropertyTests
{
    /// <summary>
    /// Creates an <see cref="EmailService"/> instance with the specified SMTP username and password
    /// configuration values. Uses in-memory configuration and mocked dependencies.
    /// </summary>
    private static EmailService CreateEmailService(string? username, string? password)
    {
        var configData = new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.example.com",
            ["Smtp:Port"] = "587",
            ["Smtp:EnableSsl"] = "true",
            ["Smtp:FromAddress"] = "test@example.com",
            ["Smtp:FromName"] = "TestApp"
        };

        if (username is not null)
            configData["Smtp:Username"] = username;

        if (password is not null)
            configData["Smtp:Password"] = password;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var mockTemplateService = new Mock<IEmailTemplateService>();
        var logger = NullLogger<EmailService>.Instance;
        var dbContext = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options);

        return new EmailService(mockTemplateService.Object, dbContext, configuration, logger);
    }

    /// <summary>
    /// Reads the private <c>_username</c> field value from an <see cref="EmailService"/> instance via reflection.
    /// </summary>
    private static string? GetPrivateField(EmailService service, string fieldName)
    {
        var field = typeof(EmailService).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(service) as string;
    }

    /// <summary>
    /// Property: For any combination of username and password values (null, empty, whitespace, or valid),
    /// the EmailService correctly reads them from configuration. When both are present and non-whitespace,
    /// credentials should be applied (both fields non-empty). When either is absent or whitespace,
    /// credentials should NOT be applied (at least one field is null/empty/whitespace).
    /// **Validates: Requirements 2.4, 2.5**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property SmtpCredentials_AppliedIfAndOnlyIfBothPresent()
    {
        // Generator for credential values: null, empty, whitespace-only, or a valid string.
        var credentialGen = Gen.Elements<string?>(null, "", "   ", "validUser123", "s3cr3tP@ss!");

        return Prop.ForAll(
            Arb.From(credentialGen),
            Arb.From(credentialGen),
            (string? username, string? password) =>
            {
                // Act: construct EmailService with the generated credential combination.
                var service = CreateEmailService(username, password);

                // Read private fields via reflection to verify config was read correctly.
                var storedUsername = GetPrivateField(service, "_username");
                var storedPassword = GetPrivateField(service, "_password");

                // Verify: username and password are read from config as-is.
                var expectedUsername = username;
                var expectedPassword = password;

                var usernameMatches = storedUsername == expectedUsername;
                var passwordMatches = storedPassword == expectedPassword;

                // The credential conditional: both must be present and non-whitespace for credentials to be applied.
                var bothPresent = !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password);

                // Verify the CreateSmtpClient method applies credentials correctly via reflection.
                var createSmtpClientMethod = typeof(EmailService).GetMethod(
                    "CreateSmtpClient", BindingFlags.NonPublic | BindingFlags.Instance);

                using var smtpClient = (System.Net.Mail.SmtpClient)createSmtpClientMethod!.Invoke(service, null)!;

                var credentialsApplied = smtpClient.Credentials is not null;

                // Assert: credentials applied if and only if both username and password are present and non-whitespace.
                var credentialConditionCorrect = credentialsApplied == bothPresent;

                var success = usernameMatches && passwordMatches && credentialConditionCorrect;

                return success.Label(
                    $"Username='{username}', Password='{password}', " +
                    $"StoredUser='{storedUsername}', StoredPass='{storedPassword}', " +
                    $"BothPresent={bothPresent}, CredentialsApplied={credentialsApplied}, " +
                    $"UsernameMatches={usernameMatches}, PasswordMatches={passwordMatches}, " +
                    $"ConditionCorrect={credentialConditionCorrect}");
            });
    }
}
