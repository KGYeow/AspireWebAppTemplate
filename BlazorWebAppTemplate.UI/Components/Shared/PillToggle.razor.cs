using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace BlazorWebAppTemplate.UI.Components.Shared;

/// <summary>
/// A generic pill-shaped toggle component that wraps <see cref="MudToggleGroup{T}"/>
/// with a rounded pill appearance. Each item renders as a circular button within the pill.
/// </summary>
/// <typeparam name="T">The type of value each toggle item represents (e.g., an enum).</typeparam>
/// <remarks>
/// <para>Basic usage with an enum:</para>
/// <code>
/// &lt;PillToggle T="ThemePreference" @bind-Value="ThemeValue"&gt;
///     &lt;PillToggleItem Value="ThemePreference.Light" Title="Light"&gt;
///         &lt;MudIcon Icon="@Icons.Material.Outlined.LightMode" Size="Size.Small" /&gt;
///     &lt;/PillToggleItem&gt;
///     &lt;PillToggleItem Value="ThemePreference.Dark" Title="Dark"&gt;
///         &lt;MudIcon Icon="@Icons.Material.Outlined.DarkMode" Size="Size.Small" /&gt;
///     &lt;/PillToggleItem&gt;
///     &lt;PillToggleItem Value="ThemePreference.System" Title="System"&gt;
///         &lt;MudIcon Icon="@Icons.Material.Outlined.SettingsBrightness" Size="Size.Small" /&gt;
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
    /// Use <see cref="PillToggleItem{T}"/> or <see cref="MudToggleItem{T}"/> as children.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
