using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.Core.Application.Abstractions;
using AspireWebAppTemplate.Core.Application.Services;
using AspireWebAppTemplate.Web;
using AspireWebAppTemplate.Web.Components;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using MudBlazor.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

// Authentication: cookie-based (the Web project sets cookies after API validates credentials)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Blazor Server auth state provider (reads from the cookie on the circuit)
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

builder.Services.AddHttpContextAccessor();

// HTTP client services (call ApiService via Aspire service discovery)
builder.Services.AddHttpClient<WeatherApiClient>(client =>
    client.BaseAddress = new("https+http://apiservice"));

builder.Services.AddHttpClient<ApiAuthService>(client =>
    client.BaseAddress = new("https+http://apiservice"));
builder.Services.AddHttpClient<ApiUserService>(client =>
    client.BaseAddress = new("https+http://apiservice"));
builder.Services.AddHttpClient<ApiRoleService>(client =>
    client.BaseAddress = new("https+http://apiservice"));
builder.Services.AddHttpClient<ApiAuditLogService>(client =>
    client.BaseAddress = new("https+http://apiservice"));

// Application services (frontend-only, no API calls)
builder.Services.AddSingleton<INavigationProvider, DefaultNavigationProvider>();
builder.Services.AddSingleton<ITimeZoneService, TimeZoneService>();
builder.Services.AddScoped<IUserTimeZoneContext, UserTimeZoneContext>();
builder.Services.AddScoped<IThemeStateService, ThemeStateService>();

// MudBlazor
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.VisibleStateDuration = 4000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.MaximumOpacity = 90;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

// ─── Minimal API endpoints for auth (must be real HTTP requests, not on SignalR) ───

// PerformLogin: called after successful API login — sets the auth cookie from token claims
app.MapGet("/Account/PerformLogin", async (HttpContext context, string? token, ApiAuthService authService) =>
{
    if (string.IsNullOrEmpty(token))
        return Results.Redirect("/Account/Login");

    var result = await authService.ValidateLoginTokenAsync(token);
    if (result is null)
        return Results.Redirect("/Account/Login");

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, result.UserId),
        new(ClaimTypes.Name, result.UserName),
        new(ClaimTypes.Email, result.Email ?? ""),
    };

    foreach (var role in result.Roles)
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
            IsPersistent = result.RememberMe,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(result.RememberMe ? 14 : 1)
        });

    return Results.Redirect(result.ReturnUrl ?? "/");
});

// Logout: clears the auth cookie
app.MapGet("/Account/Logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/Account/Login");
});

app.MapPost("/Account/Logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/Account/Login");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
