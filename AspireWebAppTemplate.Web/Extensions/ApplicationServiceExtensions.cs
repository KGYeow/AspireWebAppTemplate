using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.Core.Application.Abstractions;
using AspireWebAppTemplate.Core.Application.Services;
using AspireWebAppTemplate.Web.Abstractions;
using AspireWebAppTemplate.Web.Services;

namespace AspireWebAppTemplate.Web.Extensions;

/// <summary>
/// Extension methods for registering frontend-only application services
/// that do not communicate with the ApiService (navigation, theme, permissions context, etc.).
/// </summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Registers all frontend-only application services including navigation,
    /// time zone, theme, page permissions, and notification context services.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<INavigationProvider, DefaultNavigationProvider>();
        services.AddSingleton<ITimeZoneService, TimeZoneService>();
        services.AddScoped<IUserTimeZoneContext, UserTimeZoneContext>();
        services.AddScoped<IThemeContext, ThemeContext>();
        services.AddScoped<IPagePermissionContext, PagePermissionContext>();
        services.AddScoped<INotificationContext, NotificationContext>();
        services.AddScoped<CircuitUserContext>();

        return services;
    }
}
