using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.UI.Components.Shared;

/// <summary>
/// A reusable alert component that wraps MudBlazor's MudAlert with self-hiding behavior,
/// consistent styling defaults, and two-way binding support for the message text.
/// When <see cref="Message"/> is null or empty, no markup is rendered to the DOM.
/// </summary>
/// <remarks>
/// <para>Basic dismissible error alert with two-way binding:</para>
/// <code>
/// &lt;StatusAlert @bind-Message="_errorMessage" /&gt;
/// </code>
/// <para>Success alert without dismiss icon:</para>
/// <code>
/// &lt;StatusAlert @bind-Message="_successMessage" Severity="Severity.Success" Dismissible="false" /&gt;
/// </code>
/// <para>Dense alert for use within dialogs:</para>
/// <code>
/// &lt;StatusAlert @bind-Message="_dialogError" Dense="true" Dismissible="false" /&gt;
/// </code>
/// <para>Rich content with nested markup:</para>
/// <code>
/// &lt;StatusAlert @bind-Message="_alertMessage" Severity="Severity.Warning"&gt;
///     &lt;MudText&gt;Please &lt;b&gt;review&lt;/b&gt; the following items.&lt;/MudText&gt;
/// &lt;/StatusAlert&gt;
/// </code>
/// </remarks>
public partial class StatusAlert : ComponentBase
{
    /// <summary>
    /// The alert text content. When null or empty, the component renders nothing.
    /// Supports two-way binding via <c>@bind-Message</c>.
    /// </summary>
    [Parameter] public string? Message { get; set; }

    /// <summary>
    /// Callback invoked when the message value changes (e.g., on dismiss).
    /// Enables two-way binding with <c>@bind-Message</c> syntax.
    /// Invoked with <c>null</c> when the close icon is clicked.
    /// </summary>
    [Parameter] public EventCallback<string?> MessageChanged { get; set; }

    /// <summary>
    /// The MudBlazor severity level controlling the alert's color and icon.
    /// Defaults to <see cref="MudBlazor.Severity.Error"/>.
    /// </summary>
    [Parameter] public Severity Severity { get; set; } = Severity.Error;

    /// <summary>
    /// Whether the alert displays a close icon that clears the message on click.
    /// Defaults to <c>true</c>.
    /// </summary>
    [Parameter] public bool Dismissible { get; set; } = true;

    /// <summary>
    /// Enables compact rendering mode intended for use within dialogs.
    /// Defaults to <c>false</c>.
    /// </summary>
    [Parameter] public bool Dense { get; set; } = false;

    /// <summary>
    /// Additional CSS classes applied to the underlying MudAlert.
    /// </summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>
    /// Optional rich markup rendered inside the alert body.
    /// When provided, takes precedence over the <see cref="Message"/> text for body content.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Computes the CSS class string applied to the underlying MudAlert.
    /// Always includes <c>border-1</c>; appends <see cref="Class"/> when non-null.
    /// </summary>
    internal string ComputedClass
    {
        get
        {
            var css = "border-1";

            if (!string.IsNullOrEmpty(Class))
            {
                css = $"{css} {Class}";
            }

            return css;
        }
    }
}
