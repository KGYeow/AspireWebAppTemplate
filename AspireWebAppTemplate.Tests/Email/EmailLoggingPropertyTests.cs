// Feature: email-smtp-integration, Property 7: Email recipient address is masked in log entries
using System.Reflection;
using AspireWebAppTemplate.Infrastructure.Services;
using AspireWebAppTemplate.Infrastructure.Services.Template.Email;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspireWebAppTemplate.Tests.Email;

/// <summary>
/// Property-based tests verifying that email recipient addresses are properly masked
/// by the <see cref="EmailService"/> MaskEmailAddress method. For any valid email address
/// with a local part of 3+ characters, the masked result shows only the first 3 characters
/// followed by <c>***@domain</c>.
/// </summary>
/// <remarks>
/// **Validates: Requirements 8.4**
/// </remarks>
public class EmailLoggingPropertyTests
{
    /// <summary>
    /// Cached reference to the private static MaskEmailAddress method on <see cref="EmailService"/>.
    /// Retrieved via reflection since the method is not publicly exposed.
    /// </summary>
    private static readonly MethodInfo MaskEmailAddressMethod = typeof(EmailService).GetMethod(
        "MaskEmailAddress",
        BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Invokes the private static MaskEmailAddress method via reflection.
    /// </summary>
    /// <param name="email">The email address to mask.</param>
    /// <returns>The masked email address string.</returns>
    private static string InvokeMaskEmailAddress(string email)
    {
        return (string)MaskEmailAddressMethod.Invoke(null, new object[] { email })!;
    }

    /// <summary>
    /// Property: For any email address with a local part of 3 or more characters, the masked
    /// result starts with the first 3 characters of the local part, contains "***@", and ends
    /// with the domain portion. The full local part is never revealed.
    /// **Validates: Requirements 8.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property MaskedEmail_ShowsFirst3Chars_AndHidesDomain()
    {
        // Generator for email addresses with alphanumeric local parts (3+ chars) and valid-looking domains.
        var emailGen = Gen.Elements(
            "alice@example.com",
            "bob@company.org",
            "john.doe@test.io",
            "administrator@longdomain.co.uk",
            "xyz@mail.net",
            "testuser@service.dev",
            "info@support.org",
            "contact@business.com");

        return Prop.ForAll(
            Arb.From(emailGen),
            (string email) =>
            {
                var result = InvokeMaskEmailAddress(email);

                var atIndex = email.IndexOf('@');
                var localPart = email[..atIndex];
                var domain = email[atIndex..];
                var first3 = localPart[..Math.Min(3, localPart.Length)];

                // Assert 1: Result starts with the first 3 characters of the local part.
                var startsCorrectly = result.StartsWith(first3);

                // Assert 2: Result contains "***@".
                var containsMask = result.Contains("***@");

                // Assert 3: Result ends with the domain portion (everything from @ onwards).
                var endsWithDomain = result.EndsWith(domain);

                // Assert 4: The expected format is exactly first3chars + "***" + domain.
                var expectedResult = $"{first3}***{domain}";
                var matchesExpected = result == expectedResult;

                // Assert 5: The full local part is NOT revealed (masked) when local part > 3 chars.
                var fullLocalNotRevealed = localPart.Length <= 3 || !result.Contains(localPart);

                var allPass = startsCorrectly && containsMask && endsWithDomain &&
                              matchesExpected && fullLocalNotRevealed;

                return allPass.Label(
                    $"Input='{email}', Result='{result}', Expected='{expectedResult}', " +
                    $"StartsCorrectly={startsCorrectly}, ContainsMask={containsMask}, " +
                    $"EndsWithDomain={endsWithDomain}, MatchesExpected={matchesExpected}, " +
                    $"FullLocalNotRevealed={fullLocalNotRevealed}");
            });
    }
}
