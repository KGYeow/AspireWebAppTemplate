using AspireWebAppTemplate.UI.Theme;
using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.Web.Components.Layout;

/// <summary>
/// Minimal layout for authentication pages (Login, Register, ForgotPassword, etc.).
/// Provides only MudBlazor providers without the full app shell
/// (no topbar, drawer, nav, or footer).
/// </summary>
public partial class AuthLayout : LayoutComponentBase
{
    #region Fields / Properties

    /// <summary>
    /// Application theme instance used by <c>MudThemeProvider</c>.
    /// </summary>
    protected JabilTheme AppTheme { get; } = new();

    #endregion
}