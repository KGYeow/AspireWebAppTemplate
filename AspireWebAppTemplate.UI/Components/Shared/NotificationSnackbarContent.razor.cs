using AspireWebAppTemplate.UI.Utilities;
using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.UI.Components.Shared;

/// <summary>
/// Custom Blazor component rendered inside MudBlazor's snackbar system for rich notification display.
/// Displays a category-colored avatar icon, bold title, and caption message in a horizontal layout.
/// This component is purely presentational — click handling is managed by the snackbar's OnClick configuration.
/// </summary>
public partial class NotificationSnackbarContent : ComponentBase
{
    #region Parameters

    /// <summary>
    /// The notification title to display. Truncated to 100 characters if exceeded.
    /// </summary>
    [Parameter] public string Title { get; set; } = "";

    /// <summary>
    /// The notification message body. Truncated to 200 characters if exceeded.
    /// </summary>
    [Parameter] public string Message { get; set; } = "";

    /// <summary>
    /// The notification category string (Account, Activity, System).
    /// Determines the avatar icon and color.
    /// </summary>
    [Parameter] public string Category { get; set; } = "";

    #endregion

    #region Computed Properties

    /// <summary>
    /// The title text truncated to the maximum allowed length for snackbar display.
    /// </summary>
    private string DisplayTitle => SnackbarTextHelper.TruncateTitle(Title);

    /// <summary>
    /// The message text truncated to the maximum allowed length for snackbar display.
    /// </summary>
    private string DisplayMessage => SnackbarTextHelper.TruncateMessage(Message);

    /// <summary>
    /// The Material Symbols icon string for the notification category.
    /// </summary>
    private string CategoryIcon => NotificationCategoryHelper.GetIcon(Category);

    /// <summary>
    /// The MudBlazor CSS class for the notification category color.
    /// </summary>
    private string CategoryColorClass => NotificationCategoryHelper.GetColorClass(Category);

    #endregion
}
