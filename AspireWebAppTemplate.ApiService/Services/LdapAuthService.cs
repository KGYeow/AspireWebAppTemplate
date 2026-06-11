using System.DirectoryServices;
using System.DirectoryServices.Protocols;
using System.Net;
using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AspireWebAppTemplate.Services;

/// <summary>
/// Implements <see cref="ILdapAuthService"/> using LDAPS to authenticate users
/// against Active Directory and retrieve their attributes.
/// </summary>
/// <remarks>
/// [LDAP] This service is part of the LDAP integration. Remove it if LDAP is not needed.
/// </remarks>
public sealed class LdapAuthService : ILdapAuthService
{
    private readonly LdapSettings _settings;
    private readonly ILogger<LdapAuthService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LdapAuthService"/> class.
    /// </summary>
    /// <param name="settings">The LDAP configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    public LdapAuthService(IOptions<LdapSettings> settings, ILogger<LdapAuthService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<LdapAuthResult> AuthenticateAsync(string identifier, string password)
    {
        if (!_settings.Enabled)
        {
            return Task.FromResult(new LdapAuthResult
            {
                ErrorMessage = "LDAP authentication is disabled."
            });
        }

        var filter = $"(&(objectClass=user)(|(samaccountname={EscapeLdapFilter(identifier)})(mail={EscapeLdapFilter(identifier)})))";

        try
        {
            using var connection = new LdapConnection(
                new LdapDirectoryIdentifier(_settings.Server, int.Parse(_settings.Port)));

            connection.SessionOptions.SecureSocketLayer = true;
            connection.AuthType = AuthType.Basic;
            connection.SessionOptions.VerifyServerCertificate = (conn, cert) => true;

            // Try credential formats: domain prefix and plain identifier
            var credentialFormats = new NetworkCredential[]
            {
                new($"JABIL\\{identifier}", password),
                new(identifier, password)
            };

            bool bound = false;
            foreach (var credential in credentialFormats)
            {
                try
                {
                    connection.Bind(credential);
                    bound = true;
                    break;
                }
                catch (LdapException)
                {
                    // Try next format
                }
            }

            if (!bound)
            {
                return Task.FromResult(new LdapAuthResult
                {
                    ErrorMessage = "Invalid credentials."
                });
            }

            var request = new SearchRequest(
                _settings.BaseDn,
                filter,
                System.DirectoryServices.Protocols.SearchScope.Subtree,
                "displayName", "givenName", "sn", "title", "department", "mail", "samaccountname", "employeeNumber");

            var response = (SearchResponse)connection.SendRequest(request);

            if (response.Entries.Count > 0)
            {
                var entry = response.Entries[0];
                var attributes = new LdapUserAttributes
                {
                    DisplayName = GetAttribute(entry, "displayName"),
                    FirstName = GetAttribute(entry, "givenName"),
                    LastName = GetAttribute(entry, "sn"),
                    JobTitle = GetAttribute(entry, "title"),
                    Department = GetAttribute(entry, "department"),
                    Email = GetAttribute(entry, "mail"),
                    Ntid = GetAttribute(entry, "samaccountname"),
                    EmployeeNumber = GetAttribute(entry, "employeeNumber")
                };

                return Task.FromResult(new LdapAuthResult
                {
                    Succeeded = true,
                    Attributes = attributes
                });
            }

            return Task.FromResult(new LdapAuthResult
            {
                ErrorMessage = "User not found in directory."
            });
        }
        catch (LdapException ex)
        {
            _logger.LogWarning(ex, "LDAP authentication failed for {Identifier}.", identifier);
            return Task.FromResult(new LdapAuthResult
            {
                ErrorMessage = "Invalid credentials or LDAP error."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected LDAP error for {Identifier}.", identifier);
            return Task.FromResult(new LdapAuthResult
            {
                ErrorMessage = $"LDAP error: {ex.Message}"
            });
        }
    }

    /// <inheritdoc />
    public Task<LdapUserAttributes?> FetchUserAttributesAsync(string identifier)
    {
        if (!_settings.Enabled || string.IsNullOrEmpty(_settings.Path))
        {
            return Task.FromResult<LdapUserAttributes?>(null);
        }

        var filter = $"(&(objectClass=user)(|(samaccountname={EscapeLdapFilter(identifier)})(mail={EscapeLdapFilter(identifier)})))";

        try
        {
            using var entry = new DirectoryEntry(_settings.Path);
            using var searcher = new DirectorySearcher(entry)
            {
                Filter = filter
            };

            // Properties to load
            searcher.PropertiesToLoad.Add("displayName");
            searcher.PropertiesToLoad.Add("givenName");
            searcher.PropertiesToLoad.Add("sn");
            searcher.PropertiesToLoad.Add("title");
            searcher.PropertiesToLoad.Add("department");
            searcher.PropertiesToLoad.Add("mail");
            searcher.PropertiesToLoad.Add("samaccountname");
            searcher.PropertiesToLoad.Add("employeeNumber");

            var result = searcher.FindOne();
            if (result is not null)
            {
                return Task.FromResult<LdapUserAttributes?>(new LdapUserAttributes
                {
                    DisplayName = GetSearchProperty(result, "displayName"),
                    FirstName = GetSearchProperty(result, "givenName"),
                    LastName = GetSearchProperty(result, "sn"),
                    JobTitle = GetSearchProperty(result, "title"),
                    Department = GetSearchProperty(result, "department"),
                    Email = GetSearchProperty(result, "mail"),
                    Ntid = GetSearchProperty(result, "samaccountname"),
                    EmployeeNumber = GetSearchProperty(result, "employeeNumber")
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LDAP attribute fetch failed for {Identifier}.", identifier);
        }

        return Task.FromResult<LdapUserAttributes?>(null);
    }

    #region Helpers

    /// <summary>
    /// Gets an attribute value from an LDAP search response entry.
    /// </summary>
    private static string GetAttribute(SearchResultEntry entry, string name)
        => entry.Attributes[name]?[0]?.ToString() ?? string.Empty;

    /// <summary>
    /// Gets a property value from a <see cref="SearchResult"/>.
    /// </summary>
    private static string GetSearchProperty(SearchResult result, string name)
        => result.Properties[name]?.Count > 0
            ? result.Properties[name][0]?.ToString() ?? string.Empty
            : string.Empty;

    /// <summary>
    /// Escapes special characters in LDAP filter values to prevent injection.
    /// </summary>
    private static string EscapeLdapFilter(string value)
        => value
            .Replace(@"\", @"\5c")
            .Replace("*", @"\2a")
            .Replace("(", @"\28")
            .Replace(")", @"\29")
            .Replace("\0", @"\00");

    #endregion
}
