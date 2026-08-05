using AspireWebAppTemplate.Application.Contracts.Users;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using AspireWebAppTemplate.Web.Abstractions;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.UserManagement;

/// <summary>
/// User details page. Displays all information about a user account
/// organized in sections. Admin role required.
/// </summary>
public partial class Details : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// HTTP client service for user operations.
    /// </summary>
    [Inject] private ApiUserService UserService { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Provides user-aware datetime formatting in the viewer's configured time zone.
    /// </summary>
    [Inject] private IUserTimeZoneContext UserTimeZone { get; set; } = default!;

    #endregion

    #region Route Parameters

    /// <summary>
    /// The user's Identity ID from the route.
    /// </summary>
    [Parameter]
    public string UserId { get; set; } = "";

    #endregion

    #region State

    /// <summary>
    /// The loaded user DTO.
    /// </summary>
    protected UserDto? User { get; private set; }

    /// <summary>
    /// The user's assigned roles.
    /// </summary>
    protected List<string> UserRoles { get; private set; } = [];

    /// <summary>
    /// Whether data is currently loading.
    /// </summary>
    protected bool IsLoading { get; private set; } = true;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Loads the user and their roles on initialization.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var result = await UserService.GetUserAsync(UserId);
            if (result.Succeeded && result.Data is not null)
            {
                User = result.Data;
                UserRoles = User.Roles.OrderBy(r => r).ToList();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Formats a UTC DateTime in the viewer's time zone.
    /// </summary>
    protected string FormatDateTime(DateTime utcDateTime)
        => UserTimeZone.FormatDateTime(utcDateTime);

    /// <summary>
    /// Formats a nullable UTC DateTime in the viewer's time zone.
    /// </summary>
    protected string FormatDateTime(DateTime? utcDateTime, string fallback = "-")
        => UserTimeZone.FormatDateTime(utcDateTime, fallback: fallback);

    /// <summary>
    /// Formats a nullable UTC DateTimeOffset in the viewer's time zone.
    /// </summary>
    protected string FormatDateTime(DateTimeOffset? utcDateTimeOffset, string fallback = "-")
        => UserTimeZone.FormatDateTime(utcDateTimeOffset, fallback: fallback);

    #endregion
}
