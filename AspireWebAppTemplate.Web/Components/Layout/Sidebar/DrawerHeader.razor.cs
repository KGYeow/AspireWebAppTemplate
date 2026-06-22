using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.Web.Components.Layout.Sidebar;

/// <summary>
/// A header component for the sidebar drawer that displays a logo.
/// Shows a full-width logo when the drawer is open and a compact icon/logo when minimized.
/// </summary>
/// <remarks>
/// <para>
/// Uses <c>MudDrawerHeader</c> internally for consistent spacing and divider behavior.
/// </para>
/// <para>
/// The component expects two logo sources: <see cref="LogoSrc"/> for the expanded state
/// and <see cref="MiniLogoSrc"/> for the minimized/mini-variant state. If
/// <see cref="MiniLogoSrc"/> is not provided, it falls back to <see cref="LogoSrc"/>.
/// </para>
/// </remarks>
public partial class DrawerHeader : ComponentBase
{
    #region Parameters - Layout

    /// <summary>
    /// Gets or sets whether the parent drawer is currently open (expanded).
    /// When <c>true</c>, the full logo is displayed; when <c>false</c>, the mini logo is shown.
    /// </summary>
    [Parameter]
    public bool DrawerOpen { get; set; }

    /// <summary>
    /// Gets or sets whether the MudDrawerHeader uses dense padding.
    /// Defaults to <c>true</c> for a compact header.
    /// </summary>
    [Parameter]
    public bool Dense { get; set; } = true;

    #endregion

    #region Parameters - Full Logo (Drawer Open)

    /// <summary>
    /// Gets or sets the image source for the full logo displayed when the drawer is open.
    /// </summary>
    [Parameter]
    public string LogoSrc { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the height in pixels for the full logo.
    /// Defaults to 28px.
    /// </summary>
    [Parameter]
    public int LogoHeight { get; set; } = 28;

    #endregion

    #region Parameters - Mini Logo (Drawer Minimized)

    /// <summary>
    /// Gets or sets the image source for the compact logo displayed when the drawer is minimized.
    /// Falls back to <see cref="LogoSrc"/> if not specified.
    /// </summary>
    [Parameter]
    public string? MiniLogoSrc { get; set; }

    /// <summary>
    /// Gets or sets the height in pixels for the mini logo.
    /// Defaults to 24px.
    /// </summary>
    [Parameter]
    public int MiniLogoHeight { get; set; } = 24;

    #endregion

    #region Parameters - Navigation

    /// <summary>
    /// Gets or sets the hyperlink target when the logo is clicked.
    /// Defaults to the root path ("/").
    /// </summary>
    [Parameter]
    public string LogoHref { get; set; } = "/";

    /// <summary>
    /// Gets or sets the alt text for the logo images.
    /// Defaults to "Logo".
    /// </summary>
    [Parameter]
    public string Alt { get; set; } = "Logo";

    #endregion

    #region Computed

    /// <summary>
    /// Resolves the mini logo source, falling back to the full logo if not explicitly set.
    /// </summary>
    private string ResolvedMiniLogoSrc => string.IsNullOrWhiteSpace(MiniLogoSrc) ? LogoSrc : MiniLogoSrc;

    #endregion
}
