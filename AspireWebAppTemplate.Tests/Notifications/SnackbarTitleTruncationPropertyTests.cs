// Feature: notification-snackbar-popup, Property 1: Title truncation preserves content within limit
using AspireWebAppTemplate.UI.Utilities;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying that snackbar notification title truncation preserves content
/// within the 100-character limit, returning the original string when short enough
/// or the first 100 characters followed by "…" when the original exceeds the limit.
/// The output length never exceeds 101 characters.
/// </summary>
/// <remarks>
/// **Validates: Requirements 7.1, 7.2**
/// </remarks>
public class SnackbarTitleTruncationPropertyTests
{
    /// <summary>
    /// Property: For any notification title string with length &lt;= 100 characters,
    /// TruncateTitle SHALL return the original string unchanged.
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property TitlesWithinLimit_ReturnedUnchanged()
    {
        var shortTitleGen = Gen.Choose(0, 100)
            .Select(length => new string('A', length));

        return Prop.ForAll(
            Arb.From(shortTitleGen),
            (string title) =>
            {
                var result = SnackbarTextHelper.TruncateTitle(title);

                return (result == title).Label(
                    $"Title of length {title.Length} should be returned unchanged, " +
                    $"but got length {result.Length}");
            });
    }

    /// <summary>
    /// Property: For any notification title string with length &gt; 100 characters,
    /// TruncateTitle SHALL return the first 100 characters followed by "…" (U+2026 ellipsis).
    /// The output length SHALL never exceed 101 characters.
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property TitlesExceedingLimit_TruncatedWithEllipsis()
    {
        var longTitleGen = Gen.Choose(101, 500)
            .Select(length => new string('B', length));

        return Prop.ForAll(
            Arb.From(longTitleGen),
            (string title) =>
            {
                var result = SnackbarTextHelper.TruncateTitle(title);
                var expectedPrefix = title.Substring(0, 100);
                var expectedResult = expectedPrefix + "\u2026";

                return (result == expectedResult && result.Length <= 101).Label(
                    $"Title of length {title.Length} should be truncated to 101 chars (100 + ellipsis), " +
                    $"but got length {result.Length}");
            });
    }
}
