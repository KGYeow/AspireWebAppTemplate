using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.UI.Components.Shared;

/// <summary>
/// A single item within a <see cref="PillToggle{T}"/>.
/// Renders as a circular button (36x36px) with the provided content (typically an icon).
/// </summary>
/// <typeparam name="T">The type of value this item represents.</typeparam>
/// <remarks>
/// <para>Usage:</para>
/// <code>
/// &lt;PillToggleItem T="ThemePreference" Value="ThemePreference.Light" Title="Light"&gt;
///     &lt;MudIcon Icon="@Icons.Material.Outlined.LightMode" Size="Size.Small" /&gt;
/// &lt;/PillToggleItem&gt;
/// </code>
/// </remarks>
public partial class PillToggleItem<T> : ComponentBase
{
    /// <summary>
    /// The value this toggle item represents.
    /// </summary>
    [Parameter] public T? Value { get; set; }

    /// <summary>
    /// The title/label for accessibility (used as both <c>title</c> and <c>aria-label</c> attributes).
    /// </summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>
    /// When true, renders the item as a rounded square (rounded-lg) instead of a circle.
    /// When rounded, the item uses <c>flex: 1</c> to fill available width equally.
    /// Defaults to <c>false</c> (circular).
    /// </summary>
    [Parameter] public bool Rounded { get; set; }

    /// <summary>
    /// The content to render inside the toggle item (typically a <see cref="MudBlazor.MudIcon"/>).
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
