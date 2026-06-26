using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.Core.Application.Abstractions;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts.Users;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.UI.Theme;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using MudBlazor;
using AspireWebAppTemplate.Web.Abstractions;

namespace AspireWebAppTemplate.Web.Components.Layout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
    #region Injected Services

    /// <summary>
    /// Provides navigation utilities (current URI, base URI, navigation actions).
    /// </summary>
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;

    [Inject] protected INavigationProvider NavigationProvider { get; set; } = default!;

    /// <summary>
    /// JavaScript runtime for invoking browser APIs (timezone detection).
    /// </summary>
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>
    /// API auth service for loading and updating user profiles.
    /// </summary>
    [Inject] private ApiAuthService AuthService { get; set; } = default!;

    /// <summary>
    /// Scoped user time zone context, initialized once per circuit.
    /// </summary>
    [Inject] private IUserTimeZoneContext UserTimeZone { get; set; } = default!;

    /// <summary>
    /// Logger for recording timezone auto-detection warnings.
    /// </summary>
    [Inject] private ILogger<MainLayout> Logger { get; set; } = default!;

    /// <summary>
    /// Scoped theme state service for communicating dark mode changes across components.
    /// </summary>
    [Inject] private IThemeContext ThemeState { get; set; } = default!;

    /// <summary>
    /// Per-circuit page permission context. Initialized once during circuit startup so that
    /// the <see cref="PagePermissionHandler"/> and NavMenu have cached permissions available
    /// for zero-latency authorization checks.
    /// </summary>
    [Inject] private IPagePermissionContext PagePermissionContext { get; set; } = default!;

    /// <summary>
    /// Circuit-scoped user identity cache. Captures the authenticated user's claims early
    /// in the circuit lifecycle so that <see cref="UserIdentityDelegatingHandler"/> can
    /// propagate identity even after HttpContext becomes null (post-SSR).
    /// </summary>
    [Inject] private CircuitUserContext CircuitUserContext { get; set; } = default!;

    /// <summary>
    /// Provides access to the current HTTP context for reading the client's remote IP address
    /// during circuit initialization (before HttpContext becomes null post-SSR).
    /// </summary>
    [Inject] private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// Cascading authentication state provided by the Blazor auth infrastructure.
    /// Used to determine the current authenticated user for timezone auto-detection.
    /// </summary>
    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    #endregion

    #region Fields / Properties

    /// <summary>
    /// Indicates whether the navigation drawer is currently open.
    /// </summary>
    protected bool DrawerOpen { get; set; } = true;

    /// <summary>
    /// Current MudBlazor breakpoint, updated by <see cref="HandleBreakpointChanged(Breakpoint)"/>.
    /// </summary>
    protected Breakpoint CurrentBreakpoint { get; set; } = Breakpoint.Xs;

    /// <summary>
    /// Application theme instance used by <c>MudThemeProvider</c>.
    /// </summary>
    protected ApplicationTheme AppTheme { get; } = new();

    /// <summary>
    /// Custom navigation items to pass to <see cref="NavMenu"/>.
    /// Pulled from an injectable provider to keep the layout slim.
    /// </summary>
    protected IReadOnlyList<NavItem> NavItems { get; private set; } = [];

    /// <summary>
    /// Controls whether the dark palette is active. Bound to <c>MudThemeProvider.IsDarkMode</c>.
    /// </summary>
    private bool _isDarkMode;

    /// <summary>
    /// User profile loaded during initialization, reused by OnAfterRenderAsync
    /// to avoid duplicate API calls and reduce render thrashing during circuit startup.
    /// </summary>
    private UserDto? _currentUser;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Component initialization logic.
    /// </summary>
    protected override void OnInitialized()
    {
        NavItems = NavigationProvider.GetMainMenuItems();
        ThemeState.OnChange += OnThemeStateChanged;
    }

    /// <summary>
    /// Initializes the user timezone context early so child components can convert dates
    /// to UTC without race conditions. Runs before children render.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var authState = await AuthStateTask;
            if (authState.User.Identity?.IsAuthenticated != true) return;

            // Capture the authenticated user's claims and client IP into the circuit-scoped context.
            // This MUST happen before any API calls so that UserIdentityDelegatingHandler
            // can propagate identity headers even after HttpContext becomes null.
            var clientIp = HttpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
            CircuitUserContext.Initialize(authState.User, clientIp);

            // Initialize the per-circuit page permission cache early so that the
            // PagePermissionHandler and NavMenu have cached permissions available.
            // This runs independently of the user profile fetch below.
            await PagePermissionContext.InitializeAsync();

            var userResult = await AuthService.GetCurrentUserAsync();
            if (!userResult.Succeeded || userResult.Data is null) return;

            // Cache the user profile so OnAfterRenderAsync doesn't need another API call
            _currentUser = userResult.Data;

            // Apply Light/Dark theme immediately (no JS interop needed).
            // The System preference requires JS interop and is deferred to OnAfterRenderAsync.
            if (userResult.Data.Theme == ThemePreference.Dark)
            {
                _isDarkMode = true;
                ThemeState.SetDarkMode(true);
            }
            else if (userResult.Data.Theme == ThemePreference.Light)
            {
                _isDarkMode = false;
                ThemeState.SetDarkMode(false);
            }

            // Initialize the scoped user time zone context for this circuit (before children render)
            await UserTimeZone.InitializeAsync(userResult.Data.Id);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to initialize circuit services.");
        }
    }

    /// <summary>
    /// Runs after the component has rendered. On the first render, detects the browser
    /// timezone via JS interop and auto-saves it to the user's profile if their
    /// TimeZoneId is null. Also initializes the theme state based on the user's stored preference.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        try
        {
            // Use the cached user from OnInitializedAsync to avoid a duplicate API call.
            // This reduces render thrashing during circuit startup (fewer async state changes
            // means fewer render batches, reducing the risk of DOM desync on rapid refresh).
            var user = _currentUser;
            if (user is null) return;

            // Only apply theme here for the System preference (requires JS interop to detect OS preference).
            // Light/Dark are already applied in OnInitializedAsync without JS interop.
            if (user.Theme == ThemePreference.System)
            {
                await ApplyThemePreferenceAsync(user.Theme);
            }

            // Auto-detect browser timezone if user hasn't configured one
            if (user.TimeZoneId is not null) return;

            var timezoneModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/timezone.js");
            var detectedTimeZone = await timezoneModule.InvokeAsync<string?>("getBrowserTimeZone");

            if (string.IsNullOrWhiteSpace(detectedTimeZone)) return;

            // Save the detected timezone via the API
            await AuthService.UpdatePreferencesAsync(new UpdatePreferencesRequest
            {
                TimeZoneId = detectedTimeZone
            });

            // Re-initialize after auto-detection so the context has the new value
            await UserTimeZone.InitializeAsync(user.Id);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to auto-detect and save browser timezone.");
        }
    }

    /// <summary>
    /// Resolves the effective dark mode state from the user's theme preference
    /// and applies it to both the local field and the shared <see cref="IThemeContext"/>.
    /// </summary>
    private async Task ApplyThemePreferenceAsync(ThemePreference preference)
    {
        bool systemPrefersDark = false;

        if (preference == ThemePreference.System)
        {
            var themeModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/theme.js");
            systemPrefersDark = await themeModule.InvokeAsync<bool>("getSystemPrefersDark");
        }

        var isDark = preference switch
        {
            ThemePreference.Dark => true,
            ThemePreference.Light => false,
            ThemePreference.System => systemPrefersDark,
            _ => false
        };

        _isDarkMode = isDark;
        ThemeState.SetDarkMode(isDark);
        await InvokeAsync(StateHasChanged);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Called by <c>MudBreakpointProvider</c> when the viewport breakpoint changes.
    /// Updates <see cref="CurrentBreakpoint"/> and requests a re-render.
    /// </summary>
    /// <param name="bp">The new breakpoint value.</param>
    protected Task HandleBreakpointChanged(Breakpoint bp)
    {
        CurrentBreakpoint = bp;

        // Re-render when breakpoint changes to ensure the responsive layout updates.
        return InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Handles theme state changes triggered by other components (e.g., the Settings page).
    /// Updates the local dark mode field and re-renders the layout.
    /// </summary>
    private void OnThemeStateChanged()
    {
        _isDarkMode = ThemeState.IsDarkMode;
        InvokeAsync(StateHasChanged);
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Unsubscribes from the theme state change event to prevent memory leaks.
    /// </summary>
    public void Dispose()
    {
        ThemeState.OnChange -= OnThemeStateChanged;
    }

    #endregion
}
