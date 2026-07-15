using Amazon;
using Amazon.BedrockRuntime;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.ApiService.Abstractions;
using AspireWebAppTemplate.ApiService.Data.Entities;
using AspireWebAppTemplate.ApiService.Services;
using AspireWebAppTemplate.ApiService.Services.Clients;
using AspireWebAppTemplate.ApiService.Services.Handlers;
using AspireWebAppTemplate.ApiService.Services.Infrastructure;
using AspireWebAppTemplate.Core.Application.Abstractions;
using AspireWebAppTemplate.Core.Application.Services;
using Ganss.Xss;
using Microsoft.AspNetCore.Identity;

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
        #region Template

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
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IEmailSender<ApplicationUser>, EmailService>();

        // HtmlSanitizer: singleton with allowlist configuration for announcement content sanitization.
        services.AddSingleton(_ =>
        {
            var sanitizer = new HtmlSanitizer();

            // Clear defaults and configure explicit allowlist.
            // Includes tags produced by the Radzen HtmlEditor toolbar.
            sanitizer.AllowedTags.Clear();
            sanitizer.AllowedTags.Add("p");
            sanitizer.AllowedTags.Add("div");
            sanitizer.AllowedTags.Add("span");
            sanitizer.AllowedTags.Add("strong");
            sanitizer.AllowedTags.Add("b");
            sanitizer.AllowedTags.Add("em");
            sanitizer.AllowedTags.Add("i");
            sanitizer.AllowedTags.Add("u");
            sanitizer.AllowedTags.Add("ul");
            sanitizer.AllowedTags.Add("ol");
            sanitizer.AllowedTags.Add("li");
            sanitizer.AllowedTags.Add("a");
            sanitizer.AllowedTags.Add("h1");
            sanitizer.AllowedTags.Add("h2");
            sanitizer.AllowedTags.Add("h3");
            sanitizer.AllowedTags.Add("h4");
            sanitizer.AllowedTags.Add("h5");
            sanitizer.AllowedTags.Add("h6");
            sanitizer.AllowedTags.Add("br");
            sanitizer.AllowedTags.Add("blockquote");

            // Allow href (for links) and style (for Radzen inline formatting).
            sanitizer.AllowedAttributes.Clear();
            sanitizer.AllowedAttributes.Add("href");
            sanitizer.AllowedAttributes.Add("style");

            // Remove javascript: URI scheme from href attributes.
            sanitizer.AllowedSchemes.Clear();
            sanitizer.AllowedSchemes.Add("http");
            sanitizer.AllowedSchemes.Add("https");
            sanitizer.AllowedSchemes.Add("mailto");

            // Allow CSS properties used by Radzen HtmlEditor for inline formatting.
            sanitizer.AllowedCssProperties.Clear();
            sanitizer.AllowedCssProperties.Add("font-weight");
            sanitizer.AllowedCssProperties.Add("font-style");
            sanitizer.AllowedCssProperties.Add("text-decoration");
            sanitizer.AllowedCssProperties.Add("text-align");

            return sanitizer;
        });

        // WebCallbackClient: typed HttpClient for API→Web notification callbacks via Aspire service discovery.
        services.AddTransient<InternalApiKeyDelegatingHandler>();
        services.AddHttpClient<WebCallbackClient>(client =>
        {
            client.BaseAddress = new Uri("https+http://webfrontend");
            client.Timeout = TimeSpan.FromSeconds(5);
        })
        .AddHttpMessageHandler<InternalApiKeyDelegatingHandler>();

        #endregion

        #region Custom

        // Register your application-specific services below this line.
        // Example:
        // services.AddScoped<IOrderService, OrderService>();
        // services.AddScoped<IInvoiceService, InvoiceService>();

        // AI Service
        services.AddSingleton<AmazonBedrockRuntimeClient>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var region = config["Ai:Region"]
                ?? throw new InvalidOperationException("Ai:Region configuration is required.");

            var accessKeyId = config["Ai:AccessKeyId"];
            var secretAccessKey = config["Ai:SecretAccessKey"];
            var sessionToken = config["Ai:SessionToken"];

            var clientConfig = new AmazonBedrockRuntimeConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(region)
            };

            AWSCredentials credentials;
            if (!string.IsNullOrEmpty(accessKeyId) && !string.IsNullOrEmpty(secretAccessKey) && !string.IsNullOrEmpty(sessionToken))
            {
                credentials = new SessionAWSCredentials(accessKeyId, secretAccessKey, sessionToken);
            }
            else if (!string.IsNullOrEmpty(accessKeyId) && !string.IsNullOrEmpty(secretAccessKey))
            {
                credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
            }
            else
            {
                credentials = FallbackCredentialsFactory.GetCredentials(clientConfig, false);
                //credentials = DefaultAWSCredentialsIdentityResolver.GetCredentialsAsync(clientConfig);
            }

            return new AmazonBedrockRuntimeClient(credentials, clientConfig);
        });
        services.AddScoped<IAiService, AiService>();

        #endregion

        return services;
    }
}
