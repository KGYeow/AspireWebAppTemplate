using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.UI.Components.Shared;

/// <summary>
/// A reusable loading overlay component that displays a spinner and optional text.
/// </summary>
/// <remarks>
/// Example usage:
/// <code>
/// &lt;LoadingOverlay Visible="@isLoading" Text="Please wait..." /&gt;
/// </code>
/// </remarks>
public partial class LoadingOverlay : ComponentBase
{
    /// <summary>
    /// Determines whether the loading overlay is visible.
    /// </summary>
    [Parameter] public bool Visible { get; set; } = true;

    /// <summary>
    /// The text displayed below the loading spinner.
    /// </summary>
    [Parameter] public string Text { get; set; } = "Loading...";
}
