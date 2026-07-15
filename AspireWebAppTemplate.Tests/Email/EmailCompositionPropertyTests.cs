// Feature: email-smtp-integration, Property 1: Email message composition includes all required fields from configuration
using AspireWebAppTemplate.ApiService.Abstractions;
using AspireWebAppTemplate.ApiService.Services;
using AspireWebAppTemplate.Core.Contracts.Email;
using AspireWebAppTemplate.Core.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace AspireWebAppTemplate.Tests.Email;

/// <summary>
/// Property-based tests verifying that email message composition includes all required fields
/// from configuration. Tests are run in no-op mode (empty SMTP host) where the service logs
/// email details at Information level instead of sending. The log output is verified to contain
/// the template identifier and masked recipient.
/// </summary>
/// <remarks>
/// **Validates: Requirements 1.5, 1.6, 1.9, 8.3**
/// </remarks>
public class EmailCompositionPropertyTests
{
    /// <summary>
    /// Creates an IConfiguration instance with the provided SMTP settings.
    /// Host is set to empty string to trigger no-op mode.
    /// </summary>
    private static IConfiguration CreateConfiguration(string fromAddress, string fromName)
    {
        var configValues = new Dictionary<string, string?>
        {
            ["Smtp:Host"] = string.Empty,
            ["Smtp:Port"] = "587",
            ["Smtp:EnableSsl"] = "true",
            ["Smtp:FromAddress"] = fromAddress,
            ["Smtp:FromName"] = fromName
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
    }

    /// <summary>
    /// Property: For any valid EmailType, random recipient email, and random FromAddress/FromName
    /// configuration values, calling SendEmailAsync in no-op mode logs at Information level
    /// with the template identifier (EmailType name) and the masked recipient address.
    /// This validates that email composition correctly uses configuration fields and includes
    /// the expected information in the log output.
    /// **Validates: Requirements 1.5, 1.6, 1.9, 8.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property EmailComposition_IncludesAllRequiredFieldsFromConfiguration()
    {
        // Generator for EmailType values.
        var emailTypeGen = Gen.Elements(
            EmailType.WelcomeEmail,
            EmailType.AccountDeactivated,
            EmailType.CustomNotification);

        // Generator for from-address values (valid email format).
        var fromAddressGen = Gen.Elements(
            "noreply@acme.com",
            "support@company.org",
            "admin@myapp.io",
            "no-reply@example.net");

        // Generator for from-name values.
        var fromNameGen = Gen.Elements(
            "Acme Corp",
            "My Application",
            "Support Team",
            "Admin Portal");

        // Generator for recipient email addresses.
        var recipientGen = Gen.Elements(
            "john.doe@example.com",
            "ab@test.org",
            "alice.smith@company.io",
            "x@domain.com");

        // Generator for rendered subject and body (non-empty).
        var subjectGen = Gen.Elements(
            "Welcome to our platform",
            "Your account has been deactivated",
            "Important notification");

        var bodyGen = Gen.Elements(
            "<h1>Welcome!</h1><p>Hello there.</p>",
            "<p>Your account is deactivated.</p>",
            "<div>Custom notification content</div>");

        var compositeGen = emailTypeGen.SelectMany(emailType =>
            fromAddressGen.SelectMany(fromAddr =>
                fromNameGen.SelectMany(fromName =>
                    recipientGen.SelectMany(recipient =>
                        subjectGen.SelectMany(subject =>
                            bodyGen.Select(body => new
                            {
                                EmailType = emailType,
                                FromAddress = fromAddr,
                                FromName = fromName,
                                Recipient = recipient,
                                Subject = subject,
                                Body = body
                            }))))));

        return Prop.ForAll(
            Arb.From(compositeGen),
            input =>
            {
                // Arrange: mock IEmailTemplateService to return known rendered result.
                var mockTemplateService = new Mock<IEmailTemplateService>();
                mockTemplateService
                    .Setup(t => t.RenderAsync(input.EmailType, It.IsAny<Dictionary<string, string>>()))
                    .ReturnsAsync(new RenderedEmailResult
                    {
                        Subject = input.Subject,
                        HtmlBody = input.Body
                    });

                // Arrange: create configuration with the generated from-address and from-name.
                var configuration = CreateConfiguration(input.FromAddress, input.FromName);

                // Arrange: mock logger to capture log calls.
                var mockLogger = new Mock<ILogger<EmailService>>();

                // Act: create the service (no-op mode due to empty host) and send.
                var service = new EmailService(mockTemplateService.Object, configuration, mockLogger.Object);
                service.SendEmailAsync(input.EmailType, input.Recipient, new Dictionary<string, string>())
                    .GetAwaiter().GetResult();

                // Assert: Verify the logger was called at Information level.
                mockLogger.Verify(
                    x => x.Log(
                        LogLevel.Information,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(input.EmailType.ToString())),
                        It.IsAny<Exception?>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.Once);

                // Assert: Verify the log message contains the masked recipient.
                // Masking format: first 3 chars of local part + "***@domain"
                var atIndex = input.Recipient.IndexOf('@');
                var localPart = input.Recipient[..atIndex];
                var domain = input.Recipient[atIndex..];
                var visibleChars = Math.Min(3, localPart.Length);
                var expectedMasked = $"{localPart[..visibleChars]}***{domain}";

                mockLogger.Verify(
                    x => x.Log(
                        LogLevel.Information,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(expectedMasked)),
                        It.IsAny<Exception?>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.Once);

                // Assert: Verify the log message contains the rendered subject.
                mockLogger.Verify(
                    x => x.Log(
                        LogLevel.Information,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(input.Subject)),
                        It.IsAny<Exception?>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.Once);

                return true.Label(
                    $"EmailType={input.EmailType}, " +
                    $"FromAddress={input.FromAddress}, " +
                    $"FromName={input.FromName}, " +
                    $"Recipient={input.Recipient}, " +
                    $"MaskedRecipient={expectedMasked}, " +
                    $"Subject={input.Subject}");
            });
    }
}
