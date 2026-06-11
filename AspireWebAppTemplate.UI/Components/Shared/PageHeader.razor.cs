using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.UI.Components.Shared;

/// <summary>
/// A reusable page header component that displays a title, optional subtitle,
/// and optional right-aligned content.
/// </summary>
/// <remarks>
/// <para>Simple usage with string subtitle:</para>
/// <code>
/// &lt;PageHeader Title="Dashboard" Subtitle="Manage your items and settings here." /&gt;
/// </code>
/// <para>Advanced usage with RenderFragment subtitle and right content:</para>
/// <code>
/// &lt;PageHeader Title="Users" Typo="Typo.h3" Class="mb-3"&gt;
///     &lt;RightHeaderContent&gt;
///         &lt;MudButton Color="Color.Primary"&gt;Add User&lt;/MudButton&gt;
///     &lt;/RightHeaderContent&gt;
///     &lt;SubtitleContent&gt;
///         &lt;MudStack Row AlignItems="AlignItems.Center"&gt;
///             &lt;MudIcon Icon="Icons.Material.Rounded.Info" Size="Size.Small" /&gt;
///             &lt;MudText Typo="Typo.body2"&gt;Active users in your organization.&lt;/MudText&gt;
///         &lt;/MudStack&gt;
///     &lt;/SubtitleContent&gt;
/// &lt;/PageHeader&gt;
/// </code>
/// </remarks>
public partial class PageHeader : ComponentBase
{
    /// <summary>
    /// CSS class applied to the outer container.
    /// Defaults to <c>"mb-5"</c>.
    /// </summary>
    [Parameter] public string Class { get; set; } = "mb-5";

    /// <summary>
    /// The main title displayed in the page header.
    /// </summary>
    [Parameter] public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The MudBlazor typography variant for the title.
    /// Defaults to <see cref="MudBlazor.Typo.h4"/>.
    /// </summary>
    [Parameter] public Typo TitleTypo { get; set; } = Typo.h4;

    /// <summary>
    /// Simple text subtitle displayed below the title.
    /// Ignored when <see cref="SubtitleContent"/> is provided.
    /// </summary>
    [Parameter] public string? Subtitle { get; set; }

    /// <summary>
    /// Complex subtitle content rendered below the title.
    /// Takes precedence over <see cref="Subtitle"/> when both are provided.
    /// </summary>
    [Parameter] public RenderFragment? SubtitleContent { get; set; }

    /// <summary>
    /// Optional content rendered on the right side of the header
    /// (e.g., action buttons, icons).
    /// </summary>
    [Parameter] public RenderFragment? RightHeaderContent { get; set; }
}
