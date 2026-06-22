using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.Web.Components.Layout.Topbar;

public partial class Topbar : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Provides navigation utilities (current URI, navigate to, etc.).
    /// </summary>
    [Inject]
    protected NavigationManager Nav { get; set; } = default!;

    #endregion

    #region Parameters - Content

    /// <summary>
    /// Gets or sets the title displayed in the top bar.
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the content rendered on the left side of the top bar.
    /// Typically used for buttons or toggles.
    /// </summary>
    [Parameter]
    public RenderFragment? LeftContent { get; set; }

    /// <summary>
    /// Gets or sets the content rendered on the right side of the top bar.
    /// Typically used for profile menus or additional actions.
    /// </summary>
    [Parameter]
    public RenderFragment? RightContent { get; set; }

    #endregion

    #region Parameters - Logo

    /// <summary>
    /// Gets or sets the source path of the logo image displayed in the top bar.
    /// </summary>
    [Parameter]
    public string? LogoSrc { get; set; }

    /// <summary>
    /// Gets or sets the hyperlink target for the logo.
    /// Defaults to the root path ("/").
    /// </summary>
    [Parameter]
    public string LogoHref { get; set; } = "/";

    /// <summary>
    /// Gets or sets the height of the logo image in pixels.
    /// Defaults to 28px.
    /// </summary>
    [Parameter]
    public int LogoHeight { get; set; } = 28;

    #endregion
}