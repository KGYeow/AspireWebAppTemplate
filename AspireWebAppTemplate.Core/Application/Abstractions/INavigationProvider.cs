using AspireWebAppTemplate.Core.Common;
using System.Collections.Generic;

namespace AspireWebAppTemplate.Core.Application.Abstractions;

/// <summary>
/// Provides the main navigation items for the application.
/// </summary>
public interface INavigationProvider
{
    /// <summary>
    /// Gets the main menu items in render order.
    /// </summary>
    IReadOnlyList<NavItem> GetMainMenuItems();
}