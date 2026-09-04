using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Infrastructure.Services.Template.Navigation;
using AspireWebAppTemplate.Application.Features.Template.Navigation;
using AspireWebAppTemplate.Infrastructure.Services.Template.Navigation;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Infrastructure.Services.Template.Navigation;
using Microsoft.AspNetCore.Authorization;
using AspireWebAppTemplate.Infrastructure.Services.Template.Navigation;
using Microsoft.AspNetCore.Mvc;
using AspireWebAppTemplate.Infrastructure.Services.Template.Navigation;

namespace AspireWebAppTemplate.ApiService.Controllers.Template;

/// <summary>
/// Provides the filtered navigation tree endpoint for authenticated users.
/// This controller is intentionally thin — it handles HTTP concerns only and delegates
/// all filtering logic (auth, permissions, group visibility, orphan decoration removal)
/// to <see cref="INavigationService"/>.
/// </summary>
[Route("api/navigation")]
[Authorize]
public class NavigationController : BaseController
{
    #region Constructor

    private readonly INavigationService _navigationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationController"/> class.
    /// </summary>
    /// <param name="navigationService">The navigation service for retrieving the filtered navigation tree.</param>
    public NavigationController(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    #endregion

    #region Navigation

    /// <summary>
    /// Returns the pre-filtered navigation tree for the authenticated user.
    /// The tree includes only items the user is permitted to see based on their
    /// authentication state and page permissions.
    /// </summary>
    /// <returns>A filtered list of navigation items ready for rendering.</returns>
    /// <response code="200">Returns the filtered navigation tree as a JSON array of NavItem objects.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<NavItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<NavItem>>> GetNavigation()
    {
        var result = await _navigationService.GetFilteredNavigationAsync();
        return Ok(result);
    }

    #endregion
}
