using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.Web.Components.Pages.Account.Settings;

/// <summary>
/// Shared layout for all settings sub-pages. Provides the left navigation menu
/// and a card container for the active section's content.
/// </summary>
[Authorize]
public partial class SettingsLayout : LayoutComponentBase
{
    #region State

    /// <summary>
    /// Whether the layout is loading. Always false since individual sections handle their own loading.
    /// </summary>
    private bool _isLoading = false;

    #endregion
}
