// Feature: notification-snackbar-popup, Property 2: Message truncation preserves content within limit
using AspireWebAppTemplate.UI.Utilities;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying that notification message truncation preserves content
/// within the 200-character limit, returning the original string when short enough
/// or the first 200 characters followed by "…" when the original exceeds the limit.
/// </summary>
/// <remarks>
/// **Validates: Requirements 7.3, 7.4**
/// </remarks>
public class SnackbarMessageTruncationPropertyTests
{
    /// <summary>
    /// Property: For any notification message string with length &lt;= 200 characters,
    /// TruncateMessage SHALL return the original string unchanged.
    /// **Validates: Requirements 7.3, 7.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property MessagesWithinLimit_ReturnedUnchanged()
    {
        var shortMessageGen = Gen.Choose(0, 200)
            .Select(length => new string('A', length));

        return Prop.ForAll(
            Arb.From(shortMessageGen),
            (string message) =>
            {
                var result = SnackbarTextHelper.TruncateMessage(message);

                return (result == message).Label(
                    $"Message of length {message.Length} should be returned unchanged, " +
                    $"but got length {result.Length}");
            });
    }

    /// <summary>
    /// Property: For any notification message string with length &gt; 200 characters,
    /// TruncateMessage SHALL return the first 200 characters followed by "…" (U+2026 ellipsis).
    /// The output length SHALL never exceed 201 characters.
    /// **Validates: Requirements 7.3, 7.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property MessagesExceedingLimit_TruncatedWithEllipsis()
    {
        var longMessageGen = Gen.Choose(201, 1000)
            .Select(length => new string('B', length));

        return Prop.ForAll(
            Arb.From(longMessageGen),
            (string message) =>
            {
                var result = SnackbarTextHelper.TruncateMessage(message);
                var expectedPrefix = message.Substring(0, 200);
                var expectedResult = expectedPrefix + "\u2026";

                return (result == expectedResult).Label(
                    $"Message of length {message.Length} should be truncated to 201 chars (200 + ellipsis), " +
                    $"but got length {result.Length}");
            });
    }
}
