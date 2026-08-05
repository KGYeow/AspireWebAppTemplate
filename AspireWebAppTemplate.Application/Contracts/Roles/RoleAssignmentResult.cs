namespace AspireWebAppTemplate.Application.Contracts.Roles;

/// <summary>
/// Result of a bulk user-to-role assignment operation.
/// Returned by the role assignment endpoint to indicate how many users
/// were successfully assigned and how many failed.
/// </summary>
public sealed class RoleAssignmentResult
{
    /// <summary>
    /// The number of users successfully assigned to the role.
    /// </summary>
    public int Success { get; set; }

    /// <summary>
    /// The number of users that failed to be assigned to the role.
    /// </summary>
    public int Failed { get; set; }
}
