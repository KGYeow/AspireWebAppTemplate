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
    /// <summary>
    /// Maps authentication endpoints: PerformLogin (cookie sign-in) and Logout (cookie clear).
    /// </summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/Account");

        // PerformLogin: called after successful API login — exchanges a single-use token for an auth cookie
        group.MapGet("/PerformLogin", async (HttpContext context, string? token, ApiAuthService authService) =>
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
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(result.Data.RememberMe ? 7 : 0)
                });

            return Results.Redirect(result.Data.ReturnUrl ?? "/");
        });

        // Logout: clears the auth cookie and redirects to login
        group.MapGet("/Logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/Account/Login");
        });

        group.MapPost("/Logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/Account/Login");
        });

        return app;
    }
}
