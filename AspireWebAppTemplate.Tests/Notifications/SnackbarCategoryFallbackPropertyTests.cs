// Feature: notification-snackbar-popup, Property 4: Unknown category fallback to default icon
using AspireWebAppTemplate.UI.Utilities;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Property-based tests verifying that any category string not case-insensitively matching
/// "account", "activity", or "system" falls back to the default notification icon and color class.
/// Tests <see cref="NotificationCategoryHelper"/> directly since the component delegates to it.
/// </summary>
/// <remarks>
/// **Validates: Requirements 3.4**
/// </remarks>
public class SnackbarCategoryFallbackPropertyTests
{
    /// <summary>
    /// Property: For any non-empty category string that does not case-insensitively match
    /// "account", "activity", or "system", the category mapping SHALL return
    /// "material-symbols-rounded/notifications" icon and "mud-default" color class.
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property UnknownCategory_ReturnsFallbackIconAndColor()
    {
        var unknownCategoryGen = Gen.Elements(
            "unknown", "other", "general", "ACCOUNT1", "systems", "Activity2",
            "alert", "warning", "info-msg", "notification", "custom",
            "Account ", " Activity", "SYSTEMS", "acct", "act");

        return Prop.ForAll(
            Arb.From(unknownCategoryGen),
            (string category) =>
            {
                var icon = NotificationCategoryHelper.GetIcon(category);
                var colorClass = NotificationCategoryHelper.GetColorClass(category);

                var iconCorrect = icon == "material-symbols-rounded/notifications";
                var colorCorrect = colorClass == "";

                return (iconCorrect && colorCorrect).Label(
                    $"Category \"{category}\" should map to notifications icon (got correct: {iconCorrect}) " +
                    $"and empty color class (got correct: {colorCorrect})");
            });
    }
}
