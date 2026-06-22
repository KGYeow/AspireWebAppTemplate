using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.UI.Components.Shared;

/// <summary>
/// A wrapper component that manages the page-level loading state, showing a loading indicator
/// while data is being fetched and rendering the page content once loading completes.
/// </summary>
/// <remarks>
/// <para><b>When to use:</b></para>
/// <list type="bullet">
///   <item>Form or detail pages that fetch data in <c>OnInitializedAsync</c>.</item>
///   <item>Dashboard pages with an overall loading state before cards populate.</item>
///   <item>Any page without a component that has its own built-in loading indicator.</item>
/// </list>
///
/// <para><b>When NOT to use:</b></para>
/// <list type="bullet">
///   <item>Grid-dominant pages (e.g., User/Role Management, Audit Log) where <c>MudDataGrid</c>
///         already provides a built-in <c>Loading</c> state — adding <c>PageContent</c> would
///         be redundant and confusing to the user.</item>
///   <item>Static pages with no async initialization.</item>
/// </list>
///
/// <para><b>Usage:</b></para>
/// <code>
/// &lt;PageContent IsLoading="_isLoading"&gt;
///     &lt;MudText Typo="Typo.h5"&gt;Settings&lt;/MudText&gt;
///     &lt;MudPaper&gt;...&lt;/MudPaper&gt;
/// &lt;/PageContent&gt;
/// </code>
///
/// <para>
/// Optionally provide a custom loading template via <see cref="LoadingContent"/>
/// (e.g., a skeleton layout). If not provided, the default <see cref="LoadingOverlay"/> is shown.
/// </para>
/// </remarks>
public partial class PageContent : ComponentBase
{
    /// <summary>
    /// Gets or sets whether the page is currently loading data.
    /// When <c>true</c>, the loading indicator is shown instead of <see cref="ChildContent"/>.
    /// </summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>
    /// The page content to render once loading completes.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Optional custom loading template. If not provided, a centered <see cref="LoadingOverlay"/>
    /// is displayed. Use this to provide page-specific skeleton layouts.
    /// </summary>
    [Parameter]
    public RenderFragment? LoadingContent { get; set; }

    /// <summary>
    /// The text displayed on the default loading indicator when <see cref="LoadingContent"/> is not provided.
    /// Defaults to "Loading...".
    /// </summary>
    [Parameter]
    public string LoadingText { get; set; } = "Loading...";
}
