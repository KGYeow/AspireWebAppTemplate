using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace AspireWebAppTemplate.Web.Extensions;

/// <summary>
/// Extension methods for configuring TLS certificate trust for outbound HttpClient calls
/// made from the Web project (currently: Web → ApiService via Aspire service discovery).
/// </summary>
public static class HttpClientCertificateExtensions
{
    /// <summary>
    /// Configures the default HttpClient handler to trust internal corporate TLS certificates
    /// (e.g. self-signed or internal-CA certificates on IIS bindings) that are not present in
    /// the machine's trusted root certificate store. Applies to every HttpClient registered in
    /// this project, since all current outbound calls stay within the corporate network.
    /// </summary>
    /// <remarks>
    /// This intentionally bypasses standard TLS certificate validation (hostname, chain, and
    /// expiry checks) for all outbound HttpClient calls. It is safe only because every outbound
    /// call from this project targets internal, corporate-network endpoints — do not extend
    /// this project's HttpClient usage to public/external endpoints without revisiting this.
    /// </remarks>
    public static IServiceCollection AddInternalCertificateTrust(this IServiceCollection services)
    {
        services.ConfigureHttpClientDefaults(http =>
        {
            http.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = TrustInternalCertificate
            });
        });

        return services;
    }

    /// <summary>
    /// Always trusts the presented server certificate. Used to accept internal corporate
    /// certificates (self-signed or internal-CA issued) that browsers/servers on the
    /// corporate network trust implicitly but the .NET certificate store does not.
    /// </summary>
    private static bool TrustInternalCertificate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors) => true;
}