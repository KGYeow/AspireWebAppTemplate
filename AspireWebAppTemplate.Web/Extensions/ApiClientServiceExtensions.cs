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

        services.AddApiClient<ApiWeatherService>();
        services.AddApiClient<ApiAuthService>();
        services.AddApiClient<ApiUserService>();
        services.AddApiClient<ApiRoleService>();
        services.AddApiClient<ApiAuditLogService>();
        services.AddApiClient<ApiPermissionService>();
        services.AddApiClient<ApiNotificationService>();
        services.AddApiClient<ApiNavigationService>();
        services.AddApiClient<ApiAnnouncementService>();
        services.AddApiClient<ApiEmailTemplateService>();

        #endregion

        #region Business
        // Register your application-specific API client services below this line.
        // Example:
        // services.AddApiClient<ApiOrderService>();


        #endregion

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TClient"/> as a typed <see cref="HttpClient"/> whose base address
    /// is the ApiService service-discovery address and whose pipeline includes
    /// <see cref="UserIdentityDelegatingHandler"/> for identity propagation.
    /// </summary>
    /// <typeparam name="TClient">The typed API client to register.</typeparam>
    /// <param name="services">The service collection to add the client to.</param>
    /// <returns>The same service collection for chaining.</returns>
    private static IServiceCollection AddApiClient<TClient>(this IServiceCollection services)
        where TClient : class
    {
        services.AddHttpClient<TClient>(client => client.BaseAddress = new(ApiServiceBaseAddress))
            .AddHttpMessageHandler<UserIdentityDelegatingHandler>();
        return services;
    }
}
