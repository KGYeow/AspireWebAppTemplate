namespace AspireWebAppTemplate.Core.Contracts.Users;

/// <summary>
/// Represents the progress of a single user during an LDAP sync operation.
/// Streamed from the API one item at a time to enable real-time progress reporting.
/// </summary>
public sealed class LdapSyncProgressItem
{
    /// <summary>
    /// The total number of LDAP users to sync. Sent with every item for progress bar calculation.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// The current user's index (1-based) in the sync operation.
    /// </summary>
    public int Current { get; set; }

    /// <summary>
    /// The username of the user that was just processed.
    /// </summary>
    public string UserName { get; set; } = "";

    /// <summary>
    /// Whether this user was updated (true), skipped/unchanged (false), or failed (null).
    /// </summary>
    public bool? Updated { get; set; }
}
