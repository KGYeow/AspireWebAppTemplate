using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace AspireWebAppTemplate.Web.Components.Pages.Example;

/// <summary>
/// Developer-facing page that displays the authenticated user's claims, roles,
/// and identity information for debugging authentication and authorization.
/// </summary>
public partial class Auth : ComponentBase
{
    #region Injected Services

    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    #endregion

    #region State

    protected string DisplayName { get; private set; } = "Unknown";
    protected string UserSubtitle { get; private set; } = string.Empty;
    protected IReadOnlyList<ClaimInfo> Claims { get; private set; } = [];
    protected IReadOnlyList<string> Roles { get; private set; } = [];

    #endregion

    #region Lifecycle

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        var user = authState.User;

        if (user.Identity?.IsAuthenticated != true)
            return;

        DisplayName = user.FindFirst("DisplayName")?.Value
                      ?? user.Identity.Name
                      ?? "Unknown";

        var email = user.FindFirst(ClaimTypes.Email)?.Value;
        UserSubtitle = email is not null
            ? $"Signed in as {DisplayName} ({email})"
            : $"Signed in as {DisplayName}";

        Claims = user.Claims
            .Select(c => new ClaimInfo(c.Type, c.Value))
            .ToList();

        Roles = user.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();
    }

    #endregion

    #region Models

    protected sealed record ClaimInfo(string Type, string Value);

    #endregion
}
