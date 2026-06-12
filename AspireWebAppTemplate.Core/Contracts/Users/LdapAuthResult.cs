namespace AspireWebAppTemplate.Core.Contracts.Users;

/// <summary>
/// Represents the outcome of an LDAP authentication attempt.
/// </summary>
/// <remarks>
/// [LDAP] This class is part of the LDAP integration. Remove it if LDAP is not needed.
/// </remarks>
public sealed class LdapAuthResult
{
    /// <summary>
    /// Whether the LDAP authentication succeeded.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// The authenticated user's attributes. Only populated when <see cref="Succeeded"/> is <c>true</c>.
    /// </summary>
    public LdapUserAttributes? Attributes { get; init; }

    /// <summary>
    /// Error message on failure.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
