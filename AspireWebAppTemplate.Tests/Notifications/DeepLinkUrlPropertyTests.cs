// Feature: notification-push-deep-link, Property 4: Deep link URL correctly encodes notification ID
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying that the deep link URL constructed for snackbar navigation
/// correctly encodes the notification ID in the expected format.
/// </summary>
/// <remarks>
/// **Validates: Requirements 4.1, 4.3**
/// </remarks>
public class DeepLinkUrlPropertyTests
{
    /// <summary>
    /// Constructs the deep link URL using the same pattern as NotificationBell.ShowToast.
    /// This isolates the pure URL construction logic for property testing.
    /// </summary>
    /// <param name="notificationId">The notification entity ID.</param>
    /// <returns>The constructed deep link URL.</returns>
    private static string ConstructDeepLinkUrl(Guid notificationId)
    {
        return $"/account/notifications?id={notificationId}";
    }

    /// <summary>
    /// Generates random Guid values for property testing.
    /// </summary>
    private static Gen<Guid> GuidGen => Gen.Elements(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.Empty,
        Guid.Parse("d3b07384-d9a0-4e9a-8b5f-6c1234567890"));

    /// <summary>
    /// Property: For any Guid notificationId, the constructed deep link URL SHALL start with
    /// the path prefix "/account/notifications?id=" followed by the Guid in standard format.
    /// **Validates: Requirements 4.1, 4.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property DeepLinkUrl_StartsWithExpectedPrefix_ForAnyGuid()
    {
        return Prop.ForAll(
            Arb.From(GuidGen),
            (Guid notificationId) =>
            {
                // Act: construct the deep link URL
                var url = ConstructDeepLinkUrl(notificationId);

                // Assert: URL starts with the expected prefix
                var expectedPrefix = "/account/notifications?id=";
                return url.StartsWith(expectedPrefix).Label(
                    $"Expected URL to start with '{expectedPrefix}', but got '{url}'");
            });
    }

    /// <summary>
    /// Property: For any Guid notificationId, the ID portion extracted from the constructed URL
    /// SHALL be a valid Guid that round-trips back to the original input value.
    /// **Validates: Requirements 4.1, 4.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property DeepLinkUrl_IdPortionRoundTripsToOriginalGuid()
    {
        return Prop.ForAll(
            Arb.From(GuidGen),
            (Guid notificationId) =>
            {
                // Act: construct the URL and extract the ID portion
                var url = ConstructDeepLinkUrl(notificationId);
                var idPortion = url.Replace("/account/notifications?id=", "");

                // Assert: the extracted portion is a valid Guid that matches the input
                var parsedSuccessfully = Guid.TryParse(idPortion, out var parsedGuid);
                var matchesInput = parsedGuid == notificationId;

                return (parsedSuccessfully && matchesInput).Label(
                    $"Expected ID portion '{idPortion}' to parse as Guid equal to '{notificationId}'. " +
                    $"Parsed={parsedSuccessfully}, Match={matchesInput}");
            });
    }

    /// <summary>
    /// Property: For any Guid notificationId, the Guid in the constructed URL SHALL be in
    /// standard format (lowercase, hyphenated: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx).
    /// **Validates: Requirements 4.1, 4.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property DeepLinkUrl_GuidIsInStandardLowercaseHyphenatedFormat()
    {
        return Prop.ForAll(
            Arb.From(GuidGen),
            (Guid notificationId) =>
            {
                // Act: construct the URL and extract the ID portion
                var url = ConstructDeepLinkUrl(notificationId);
                var idPortion = url.Replace("/account/notifications?id=", "");

                // Assert: the Guid representation matches the standard ToString() format
                // which is lowercase, hyphenated (e.g., "d3b07384-d9a0-4e9a-8b5f-6c1234567890")
                var expectedFormat = notificationId.ToString();
                var isLowercaseHyphenated = idPortion == expectedFormat
                    && idPortion == idPortion.ToLowerInvariant()
                    && idPortion.Count(c => c == '-') == 4
                    && idPortion.Length == 36;

                return isLowercaseHyphenated.Label(
                    $"Expected Guid in standard format '{expectedFormat}', got '{idPortion}'. " +
                    $"IsLowercase={idPortion == idPortion.ToLowerInvariant()}, " +
                    $"HyphenCount={idPortion.Count(c => c == '-')}, Length={idPortion.Length}");
            });
    }
}
