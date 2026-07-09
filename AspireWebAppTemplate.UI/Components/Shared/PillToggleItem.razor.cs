using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.UI.Components.Shared;

/// <summary>
/// A single item within a <see cref="PillToggle{T}"/>.
/// Supports three shape modes and derives its height from the parent's cascaded <see cref="Size"/>.
/// </summary>
/// <typeparam name="T">The type of value this item represents.</typeparam>
/// <remarks>
/// <para>Heights match MudBlazor's MudToggleGroup item heights:</para>
/// <list type="bullet">
///   <item>Small: 30px</item>
///   <item>Medium: 36px (default)</item>
///   <item>Large: 44px</item>
/// </list>
/// <para>Usage with pill shape:</para>
/// <code>
/// &lt;PillToggleItem T="Severity" Value="Severity.Info" Title="Info" Shape="PillToggleItemShape.Pill"&gt;
///     Info
/// &lt;/PillToggleItem&gt;
/// </code>
/// </remarks>
public partial class PillToggleItem<T> : ComponentBase
{
    #region Parameters

    /// <summary>
    /// The value this toggle item represents.
    /// </summary>
    [Parameter] public T? Value { get; set; }

    /// <summary>
    /// The title/label for accessibility (used as both <c>title</c> and <c>aria-label</c> attributes).
    /// </summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>
    /// The shape of this toggle item. Defaults to <see cref="PillToggleItemShape.Circle"/>.
    /// </summary>
    [Parameter] public PillToggleItemShape Shape { get; set; } = PillToggleItemShape.Circle;

    /// <summary>
    /// Shorthand for setting <see cref="Shape"/> to <see cref="PillToggleItemShape.Rounded"/>.
    /// Maintained for backward compatibility. When true, overrides <see cref="Shape"/> to Rounded.
    /// </summary>
    [Parameter] public bool Rounded { get; set; }

    /// <summary>
    /// Additional inline styles applied to the toggle item. Use as an escape hatch for
    /// custom sizing needs not covered by the Size/Shape system.
    /// </summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>
    /// The content to render inside the toggle item (icon, text, or both).
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// The size cascaded from the parent <see cref="PillToggle{T}"/>.
    /// Used to derive height and horizontal dimensions.
    /// </summary>
    [CascadingParameter(Name = "PillToggleSize")]
    private Size ParentSize { get; set; } = Size.Medium;

    #endregion

    #region Private Methods

    /// <summary>
    /// Returns the CSS border-radius class for the item based on the effective shape.
    /// </summary>
    private string GetShapeClass() => GetEffectiveShape() switch
    {
        PillToggleItemShape.Rounded => "rounded-lg",
        PillToggleItemShape.Pill => "rounded-pill",
        _ => "rounded-circle"
    };

    /// <summary>
    /// Returns the computed inline style combining size-derived dimensions and any user-provided style.
    /// </summary>
    private string GetComputedStyle()
    {
        var height = GetHeight();
        var shape = GetEffectiveShape();

        var baseStyle = shape switch
        {
            PillToggleItemShape.Circle => $"height: {height}px; width: {height}px;",
            PillToggleItemShape.Rounded => $"height: {height}px; flex: 1;",
            PillToggleItemShape.Pill => $"height: {height}px; padding-left: {GetPillPadding()}px; padding-right: {GetPillPadding()}px;",
            _ => $"height: {height}px; width: {height}px;"
        };

        return string.IsNullOrEmpty(Style) ? baseStyle : $"{baseStyle} {Style}";
    }

    /// <summary>
    /// Returns the item height in pixels based on the parent's cascaded Size.
    /// Matches MudBlazor's MudToggleGroup item heights.
    /// </summary>
    private int GetHeight() => ParentSize switch
    {
        Size.Small => 30,
        Size.Large => 44,
        _ => 36 // Medium (default)
    };

    /// <summary>
    /// Returns the horizontal padding for pill-shaped items based on the parent's cascaded Size.
    /// </summary>
    private int GetPillPadding() => ParentSize switch
    {
        Size.Small => 12,
        Size.Large => 20,
        _ => 16 // Medium (default)
    };

    /// <summary>
    /// Resolves the effective shape, considering the backward-compatible <see cref="Rounded"/> parameter.
    /// </summary>
    private PillToggleItemShape GetEffectiveShape()
    {
        if (Rounded) return PillToggleItemShape.Rounded;
        return Shape;
    }

    #endregion
}

/// <summary>
/// Defines the shape options for a <see cref="PillToggleItem{T}"/>.
/// </summary>
public enum PillToggleItemShape
{
    /// <summary>
    /// Circular shape (equal width and height). Best for icon-only items.
    /// </summary>
    Circle,

    /// <summary>
    /// Rounded rectangle (rounded-lg corners). Uses <c>flex: 1</c> to fill available width equally.
    /// Best for text items that should share width evenly.
    /// </summary>
    Rounded,

    /// <summary>
    /// Pill/capsule shape (fully rounded ends). Uses horizontal padding for content-driven width.
    /// Best for text items that should size naturally to their content.
    /// </summary>
    Pill
}
