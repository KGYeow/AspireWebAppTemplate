using AspireWebAppTemplate.Web;
using AspireWebAppTemplate.Web.Authentication;
using AspireWebAppTemplate.Web.Authorization;
using AspireWebAppTemplate.Web.Components;
using AspireWebAppTemplate.Web.Endpoints;
using AspireWebAppTemplate.Web.Extensions;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Radzen;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Trust internal corporate TLS certificates for outbound HttpClient calls (Web → ApiService).
builder.Services.AddInternalCertificateTrust();

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
    })
    .AddScheme<AuthenticationSchemeOptions, InternalApiKeyAuthenticationHandler>(
        InternalApiKeyAuthenticationHandler.SchemeName, _ => { });

// Authorization: register the page permission handler and add PagePermissionRequirement
// to the default policy (triggered by [Authorize] in _Imports.razor).
// We do NOT set FallbackPolicy because that would block Blazor's /_blazor/negotiate and
// static asset endpoints for unauthenticated users, breaking the login page circuit.
builder.Services.AddScoped<IAuthorizationHandler, PagePermissionHandler>();
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddRequirements(new PagePermissionRequirement())
        .Build();

    // Internal API policy: used by the notification callback endpoint for service-to-service auth.
    options.AddPolicy("InternalApiPolicy", policy =>
        policy.AddAuthenticationSchemes(InternalApiKeyAuthenticationHandler.SchemeName)
              .RequireAuthenticatedUser());
});
builder.Services.AddCascadingAuthenticationState();

// Blazor Server auth state provider (reads from the cookie on the circuit)
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

builder.Services.AddHttpContextAccessor();

// Delegating handler that forwards authenticated user identity to the API service
builder.Services.AddTransient<UserIdentityDelegatingHandler>();


// HTTP client services (call ApiService via Aspire service discovery)
builder.Services.AddApiClients();

// Application services (frontend-only, no API calls)
builder.Services.AddApplicationServices();

// MudBlazor
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.VisibleStateDuration = 3000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.MaximumOpacity = 95;
});

// Radzen: required for RadzenHtmlEditor (announcement content editor).
builder.Services.AddRadzenComponents();

// SignalR: required for the NotificationHub real-time notification delivery.
builder.Services.AddSignalR();

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

// Internal notification callback endpoint (API→Web service-to-service)
app.MapNotificationCallback();

// Real-time notification hub for Blazor Server circuits
app.MapHub<AspireWebAppTemplate.Web.Hubs.NotificationHub>("/hubs/notifications");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
