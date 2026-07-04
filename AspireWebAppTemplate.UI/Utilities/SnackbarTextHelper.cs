namespace AspireWebAppTemplate.UI.Utilities;

/// <summary>
/// Provides text truncation utilities for notification snackbar content.
/// Extracted as static methods for property-based testing without Blazor rendering.
/// </summary>
public static class SnackbarTextHelper
{
    /// <summary>
    /// Maximum allowed length for notification titles in snackbar display.
    /// </summary>
    public const int MaxTitleLength = 100;

    /// <summary>
    /// Maximum allowed length for notification messages in snackbar display.
    /// </summary>
    public const int MaxMessageLength = 200;

    /// <summary>
    /// Truncates a notification title to <see cref="MaxTitleLength"/> characters,
    /// appending an ellipsis ("…") if the original exceeds the limit.
    /// Returns the original string unchanged when within the limit.
    /// Returns an empty string when the input is null or empty.
    /// </summary>
    /// <param name="title">The notification title to truncate.</param>
    /// <returns>The original or truncated title, or an empty string for null/empty input.</returns>
    public static string TruncateTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return title ?? "";
        return title.Length > MaxTitleLength
            ? string.Concat(title.AsSpan(0, MaxTitleLength), "…")
            : title;
    }

    /// <summary>
    /// Truncates a notification message to <see cref="MaxMessageLength"/> characters,
    /// appending an ellipsis ("…") if the original exceeds the limit.
    /// Returns the original string unchanged when within the limit.
    /// Returns an empty string when the input is null or empty.
    /// </summary>
    /// <param name="message">The notification message to truncate.</param>
    /// <returns>The original or truncated message, or an empty string for null/empty input.</returns>
    public static string TruncateMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return message ?? "";
        return message.Length > MaxMessageLength
            ? string.Concat(message.AsSpan(0, MaxMessageLength), "…")
            : message;
    }
}
