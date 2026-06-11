using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.Core.Application.Abstractions;
using AspireWebAppTemplate.Core.Application.Services;
using AspireWebAppTemplate.Web;
using AspireWebAppTemplate.Web.Components;
using AspireWebAppTemplate.Web.Endpoints;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using MudBlazor.Services;

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
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Blazor Server auth state provider (reads from the cookie on the circuit)
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

builder.Services.AddHttpContextAccessor();

// Delegating handler that forwards authenticated user identity to the API service
builder.Services.AddTransient<UserIdentityDelegatingHandler>();

// HTTP client services (call ApiService via Aspire service discovery)
builder.Services.AddHttpClient<ApiWeatherService>(client =>
    client.BaseAddress = new("https+http://apiservice"))
    .AddHttpMessageHandler<UserIdentityDelegatingHandler>();

builder.Services.AddHttpClient<ApiAuthService>(client =>
    client.BaseAddress = new("https+http://apiservice"))
    .AddHttpMessageHandler<UserIdentityDelegatingHandler>();
builder.Services.AddHttpClient<ApiUserService>(client =>
    client.BaseAddress = new("https+http://apiservice"))
    .AddHttpMessageHandler<UserIdentityDelegatingHandler>();
builder.Services.AddHttpClient<ApiRoleService>(client =>
    client.BaseAddress = new("https+http://apiservice"))
    .AddHttpMessageHandler<UserIdentityDelegatingHandler>();
builder.Services.AddHttpClient<ApiAuditLogService>(client =>
    client.BaseAddress = new("https+http://apiservice"))
    .AddHttpMessageHandler<UserIdentityDelegatingHandler>();

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

app.UseStatusCodePagesWithReExecute("/not-found");

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

// Auth endpoints (PerformLogin, Logout — must be real HTTP requests for cookie operations)
app.MapAuthEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
