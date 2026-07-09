using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.UI.Components.Shared;

/// <summary>
/// A generic pill-shaped toggle component that wraps <see cref="MudToggleGroup{T}"/>
/// with a rounded pill appearance. Each item renders as a circle, rounded rectangle,
/// or pill-shaped button within the container.
/// </summary>
/// <typeparam name="T">The type of value each toggle item represents (e.g., an enum).</typeparam>
/// <remarks>
/// <para>
/// Cascades a <see cref="Size"/> value to child <see cref="PillToggleItem{T}"/> components
/// so they can derive their height and spacing automatically. Heights match MudBlazor's
/// <c>MudToggleGroup</c> item heights for visual alignment with other form controls.
/// </para>
/// <para>Basic usage:</para>
/// <code>
/// &lt;PillToggle T="ThemePreference" @bind-Value="ThemeValue" Size="Size.Medium"&gt;
///     &lt;PillToggleItem Value="ThemePreference.Light" Title="Light"&gt;
///         &lt;MudIcon Icon="@Icons.Material.Outlined.LightMode" Size="Size.Small" /&gt;
///     &lt;/PillToggleItem&gt;
///     &lt;PillToggleItem Value="ThemePreference.Dark" Title="Dark"&gt;
///         &lt;MudIcon Icon="@Icons.Material.Outlined.DarkMode" Size="Size.Small" /&gt;
///     &lt;/PillToggleItem&gt;
/// &lt;/PillToggle&gt;
/// </code>
/// </remarks>
public partial class PillToggle<T> : ComponentBase
{
    /// <summary>
    /// The currently selected value.
    /// </summary>
    [Parameter] public T? Value { get; set; }

    /// <summary>
    /// Callback invoked when the selected value changes.
    /// </summary>
    [Parameter] public EventCallback<T?> ValueChanged { get; set; }

    /// <summary>
    /// The size of the toggle items. Controls height and spacing to match MudBlazor's sizing tiers.
    /// Defaults to <see cref="MudBlazor.Size.Medium"/>.
    /// </summary>
    [Parameter] public Size Size { get; set; } = Size.Medium;

    /// <summary>
    /// The MudBlazor color used for the active toggle item.
    /// Defaults to <see cref="MudBlazor.Color.Primary"/>.
    /// </summary>
    [Parameter] public Color Color { get; set; } = Color.Primary;

    /// <summary>
    /// Additional CSS classes applied to the toggle group container.
    /// </summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>
    /// Additional inline styles applied to the toggle group container.
    /// </summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>
    /// The toggle items to render inside the pill.
    /// Use <see cref="PillToggleItem{T}"/> as children.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Returns the gap between items based on the current size.
    /// </summary>
    private string GetGap() => Size switch
    {
        Size.Small => "2px",
        Size.Large => "6px",
        _ => "4px"
    };
}
