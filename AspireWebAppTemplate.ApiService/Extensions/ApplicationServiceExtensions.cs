using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.ApiService.Abstractions;
using AspireWebAppTemplate.ApiService.Services;
using AspireWebAppTemplate.Core.Application.Abstractions;
using AspireWebAppTemplate.Core.Application.Services;

namespace AspireWebAppTemplate.ApiService.Extensions;

/// <summary>
/// Extension methods for registering application-layer business services
/// (scoped and singleton) used by the API controllers.
/// </summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Registers all application business services including the current user accessor,
    /// navigation provider, and all domain service implementations.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        services.AddSingleton<INavigationProvider, DefaultNavigationProvider>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        services.AddScoped<ILoginService, LoginService>();
        services.AddScoped<IRegisterService, RegisterService>();
        services.AddScoped<IPagePermissionService, PagePermissionService>();
        services.AddScoped<ILdapAuthService, LdapAuthService>();
        services.AddScoped<ILdapLoginService, LdapLoginService>();
        services.AddScoped<INavigationService, NavigationService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
