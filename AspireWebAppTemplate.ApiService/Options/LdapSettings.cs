namespace AspireWebAppTemplate.Options;

/// <summary>
/// Strongly-typed configuration for LDAP authentication, bound from the
/// <c>"LDAP"</c> section in <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// [LDAP] This class is part of the LDAP integration. Remove it if LDAP is not needed.
/// </remarks>
public sealed class LdapSettings
{
    /// <summary>
    /// Whether LDAP authentication is enabled. When <c>false</c>, all LDAP
    /// operations return failure immediately.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The LDAP server hostname (e.g., "ldaps.corp.JABIL.ORG").
    /// </summary>
    public string Server { get; set; } = string.Empty;

    /// <summary>
    /// The LDAP server port as a string (e.g., "636" for LDAPS).
    /// </summary>
    public string Port { get; set; } = "636";

    /// <summary>
    /// The base distinguished name for LDAP searches (e.g., "DC=corp,DC=JABIL,DC=ORG").
    /// </summary>
    public string BaseDn { get; set; } = string.Empty;

    /// <summary>
    /// The NetBIOS domain name used for credential binding (e.g., "CORP").
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// The full LDAP path URI used by <c>DirectorySearcher</c> for attribute fetching
    /// (e.g., "LDAP://ldaps.corp.JABIL.ORG:636").
    /// </summary>
    public string Path { get; set; } = string.Empty;
}
