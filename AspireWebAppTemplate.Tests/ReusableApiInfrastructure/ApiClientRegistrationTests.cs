// Feature: reusable-api-infrastructure, Task 4.2: client registration resolution
using System.Reflection;
using AspireWebAppTemplate.Web.Extensions;
using AspireWebAppTemplate.Web.Services;
using AspireWebAppTemplate.Web.Services.ApiClients;
using Microsoft.AspNetCore.Http;

namespace AspireWebAppTemplate.Tests.ReusableApiInfrastructure;

/// <summary>
/// Unit tests verifying that <see cref="ApiClientServiceExtensions.AddApiClients"/> registers each
/// of the ten template API clients as a typed <see cref="HttpClient"/> whose base address is the
/// ApiService service-discovery address and whose message pipeline includes
/// <see cref="UserIdentityDelegatingHandler"/> for identity propagation (Requirements 3.2, 3.3).
/// The tests build a real <see cref="ServiceProvider"/>, supplying only the ambient dependencies the
/// delegating handler needs (<see cref="IHttpContextAccessor"/> and <see cref="CircuitUserContext"/>),
/// then resolve each client and assert its configured base address, and assert that no unexpected
/// typed client is registered beyond the ten templates.
/// </summary>
public class ApiClientRegistrationTests
{
    #region Fixture

    /// <summary>
    /// The Aspire service-discovery base address every template client is configured with. This mirrors
    /// the private <c>ApiServiceBaseAddress</c> constant in <see cref="ApiClientServiceExtensions"/>.
    /// </summary>
    private const string ApiServiceBaseAddress = "https+http://apiservice";

    /// <summary>
    /// The ten template API client types registered by <see cref="ApiClientServiceExtensions.AddApiClients"/>.
    /// </summary>
    private static readonly Type[] TemplateClientTypes =
    [
        typeof(ApiWeatherService),
        typeof(ApiAuthService),
        typeof(ApiUserService),
        typeof(ApiRoleService),
        typeof(ApiAuditLogService),
        typeof(ApiPermissionService),
        typeof(ApiNotificationService),
        typeof(ApiNavigationService),
        typeof(ApiAnnouncementService),
        typeof(ApiEmailTemplateService),
    ];

    /// <summary>
    /// Builds a service provider with the API clients registered plus the ambient dependencies the
    /// <see cref="UserIdentityDelegatingHandler"/> requires so the typed clients resolve cleanly.
    /// </summary>
    /// <returns>A fully built <see cref="ServiceProvider"/>.</returns>
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        // The typed clients depend on the delegating handler; the handler depends on these two.
        services.AddTransient<UserIdentityDelegatingHandler>();
        services.AddHttpContextAccessor();
        services.AddScoped<CircuitUserContext>();

        // System under test.
        services.AddApiClients();

        return services.BuildServiceProvider();
    }

    #endregion

    #region Resolution & Base Address

    /// <summary>
    /// Every template client resolves as its concrete type and receives an <see cref="HttpClient"/>
    /// whose <see cref="HttpClient.BaseAddress"/> is the ApiService service-discovery address.
    /// </summary>
    [Fact]
    public void EachTemplateClient_Resolves_WithApiServiceBaseAddress()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        foreach (var clientType in TemplateClientTypes)
        {
            var client = scope.ServiceProvider.GetService(clientType);
            Assert.NotNull(client);

            var httpClient = GetHttpClient(client!);
            Assert.NotNull(httpClient.BaseAddress);
            Assert.Equal(ApiServiceBaseAddress, httpClient.BaseAddress!.ToString().TrimEnd('/'));
        }
    }

    /// <summary>
    /// Each template client's named <see cref="HttpClient"/> pipeline includes the
    /// <see cref="UserIdentityDelegatingHandler"/>. The handler is verified by inspecting the built
    /// primary/delegating handler chain produced by <see cref="IHttpMessageHandlerFactory"/> for the
    /// client's default (type-named) HttpClient name.
    /// </summary>
    [Fact]
    public void EachTemplateClient_Pipeline_IncludesUserIdentityDelegatingHandler()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handlerFactory = scope.ServiceProvider.GetRequiredService<IHttpMessageHandlerFactory>();

        foreach (var clientType in TemplateClientTypes)
        {
            // Typed clients are registered under a named HttpClient whose name is the client type name.
            var handler = handlerFactory.CreateHandler(clientType.Name);

            Assert.True(
                PipelineContainsUserIdentityHandler(handler),
                $"Pipeline for '{clientType.Name}' does not contain UserIdentityDelegatingHandler.");
        }
    }

    #endregion

    #region No Unexpected Clients

    /// <summary>
    /// Only the ten template clients are registered as typed clients — no unexpected typed client is
    /// present. Verified by counting distinct <see cref="HttpClient"/>-consuming service registrations
    /// whose implementation type is one of the API client classes.
    /// </summary>
    [Fact]
    public void NoUnexpectedTypedClient_IsRegistered()
    {
        var services = new ServiceCollection();
        services.AddTransient<UserIdentityDelegatingHandler>();
        services.AddHttpContextAccessor();
        services.AddScoped<CircuitUserContext>();
        services.AddApiClients();

        // Typed clients register their concrete client type as the service type. Collect every
        // registered "Api*Service" typed client, then assert the set equals exactly the ten templates
        // — no fewer (all templates present) and no more (no unexpected typed client registered).
        var registeredApiClients = services
            .Select(d => d.ServiceType)
            .Where(t => t.IsClass
                        && t.Name.StartsWith("Api", StringComparison.Ordinal)
                        && t.Name.EndsWith("Service", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        Assert.Equal(TemplateClientTypes.Length, registeredApiClients.Count);
        foreach (var expected in TemplateClientTypes)
            Assert.Contains(expected, registeredApiClients);

        // No unexpected typed client beyond the ten templates.
        foreach (var registered in registeredApiClients)
            Assert.Contains(registered, TemplateClientTypes);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Extracts the injected <see cref="HttpClient"/> from a resolved typed API client by reading its
    /// private <c>_http</c> field (every client stores the injected client in that field).
    /// </summary>
    /// <param name="client">The resolved API client instance.</param>
    /// <returns>The <see cref="HttpClient"/> the client was constructed with.</returns>
    private static HttpClient GetHttpClient(object client)
    {
        var field = client.GetType().GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var http = field!.GetValue(client) as HttpClient;
        Assert.NotNull(http);
        return http!;
    }

    /// <summary>
    /// Walks the delegating-handler chain looking for a <see cref="UserIdentityDelegatingHandler"/>.
    /// </summary>
    /// <param name="handler">The top of the message-handler pipeline.</param>
    /// <returns><c>true</c> if the pipeline contains a <see cref="UserIdentityDelegatingHandler"/>.</returns>
    private static bool PipelineContainsUserIdentityHandler(HttpMessageHandler handler)
    {
        var current = handler;
        while (current is DelegatingHandler delegating)
        {
            if (current is UserIdentityDelegatingHandler)
                return true;
            current = delegating.InnerHandler!;
        }

        return current is UserIdentityDelegatingHandler;
    }

    #endregion
}
