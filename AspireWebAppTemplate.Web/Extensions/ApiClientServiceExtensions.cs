using AspireWebAppTemplate.Web.Services;
using AspireWebAppTemplate.Web.Services.ApiClients;

namespace AspireWebAppTemplate.Web.Extensions;

/// <summary>
/// Extension methods for registering API client HttpClient services
/// that communicate with the ApiService via Aspire service discovery.
/// </summary>
public static class ApiClientServiceExtensions
{
    /// <summary>
    /// Aspire service discovery base address for the ApiService project.
    /// </summary>
    private const string ApiServiceBaseAddress = "https+http://apiservice";

    /// <summary>
    /// Registers all typed HttpClient services for communicating with the ApiService.
    /// Each client is configured with the Aspire service discovery base address and
    /// the <see cref="UserIdentityDelegatingHandler"/> for identity propagation.
    /// </summary>
    public static IServiceCollection AddApiClients(this IServiceCollection services)
    {
        #region Template

        services.AddHttpClient<ApiWeatherService>(client =>
            client.BaseAddress = new(ApiServiceBaseAddress))
            .AddHttpMessageHandler<UserIdentityDelegatingHandler>();

        services.AddHttpClient<ApiAuthService>(client =>
            client.BaseAddress = new(ApiServiceBaseAddress))
            .AddHttpMessageHandler<UserIdentityDelegatingHandler>();

        services.AddHttpClient<ApiUserService>(client =>
            client.BaseAddress = new(ApiServiceBaseAddress))
            .AddHttpMessageHandler<UserIdentityDelegatingHandler>();

        services.AddHttpClient<ApiRoleService>(client =>
            client.BaseAddress = new(ApiServiceBaseAddress))
            .AddHttpMessageHandler<UserIdentityDelegatingHandler>();

        services.AddHttpClient<ApiAuditLogService>(client =>
            client.BaseAddress = new(ApiServiceBaseAddress))
            .AddHttpMessageHandler<UserIdentityDelegatingHandler>();

        services.AddHttpClient<ApiPagePermissionService>(client =>
            client.BaseAddress = new(ApiServiceBaseAddress))
            .AddHttpMessageHandler<UserIdentityDelegatingHandler>();

        services.AddHttpClient<ApiNotificationService>(client =>
            client.BaseAddress = new(ApiServiceBaseAddress))
            .AddHttpMessageHandler<UserIdentityDelegatingHandler>();

        services.AddHttpClient<ApiNavigationService>(client =>
            client.BaseAddress = new(ApiServiceBaseAddress))
            .AddHttpMessageHandler<UserIdentityDelegatingHandler>();

        services.AddHttpClient<ApiAnnouncementService>(client =>
            client.BaseAddress = new(ApiServiceBaseAddress))
            .AddHttpMessageHandler<UserIdentityDelegatingHandler>();

        services.AddHttpClient<ApiEmailTemplateService>(client =>
            client.BaseAddress = new(ApiServiceBaseAddress))
            .AddHttpMessageHandler<UserIdentityDelegatingHandler>();

        #endregion

        #region Custom

        // Register your application-specific API client services below this line.
        // Example:
        // services.AddHttpClient<ApiOrderService>(client =>
        //     client.BaseAddress = new(ApiServiceBaseAddress))
        //     .AddHttpMessageHandler<UserIdentityDelegatingHandler>();

        services.AddHttpClient<ApiAiService>(client =>
            client.BaseAddress = new(ApiServiceBaseAddress))
            .AddHttpMessageHandler<UserIdentityDelegatingHandler>();

        #endregion

        return services;
    }
}
