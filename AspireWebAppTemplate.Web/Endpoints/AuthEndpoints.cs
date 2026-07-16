using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace AspireWebAppTemplate.Web.Endpoints;

/// <summary>
/// Minimal API endpoints for authentication operations that must execute as real HTTP requests
/// (not on a SignalR circuit). These handle cookie sign-in/sign-out which requires an actual
/// HTTP response to set/clear browser cookies.
/// </summary>
public static class AuthEndpoints
{
    #region Endpoint Registration

    /// <summary>
    /// Maps authentication endpoints: PerformLogin (cookie sign-in) and Logout (cookie clear).
    /// </summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/Account");

        group.MapGet("/PerformLogin", HandlePerformLogin);
        group.MapGet("/Logout", HandleLogoutGet);
        group.MapPost("/Logout", HandleLogoutPost);

        return app;
    }

    #endregion

    #region Request Handling

    /// <summary>
    /// Exchanges a single-use login token for an authentication cookie.
    /// Called after successful API login to establish the browser session.
    /// </summary>
    private static async Task<IResult> HandlePerformLogin(HttpContext context, string? token, ApiAuthService authService)
    {
        if (string.IsNullOrEmpty(token))
            return Results.Redirect("/Account/Login");

        var result = await authService.ValidateLoginTokenAsync(token);
        if (!result.Succeeded || result.Data is null)
            return Results.Redirect("/Account/Login");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Data.UserId),
            new(ClaimTypes.Name, result.Data.UserName),
            new(ClaimTypes.Email, result.Data.Email ?? ""),
            new("DisplayName", result.Data.DisplayName ?? result.Data.UserName),
        };

        foreach (var role in result.Data.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = result.Data.RememberMe,
                ExpiresUtc = result.Data.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : null
            });

        return Results.Redirect(result.Data.ReturnUrl ?? "/");
    }

    /// <summary>
    /// Clears the authentication cookie and redirects to the login page (GET).
    /// Uses HttpContext.Response.Redirect directly to bypass Blazor's enhanced navigation
    /// interception which can cause a blank page in InteractiveServer mode.
    /// </summary>
    private static async Task HandleLogoutGet(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Response.Redirect("/Account/Login");
    }

    /// <summary>
    /// Clears the authentication cookie and redirects to the login page (POST).
    /// Uses HttpContext.Response.Redirect directly to bypass Blazor's enhanced navigation.
    /// </summary>
    private static async Task HandleLogoutPost(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Response.Redirect("/Account/Login");
    }

    #endregion
}
