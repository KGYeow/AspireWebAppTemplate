namespace AspireWebAppTemplate.Application.Features.Template.Roles;

/// <summary>
/// Request payload for creating a new role in the system.
/// Sent from the admin UI to the Roles API endpoint.
/// </summary>
public sealed class CreateRoleRequest
{
    /// <summary>
    /// The unique name identifier for the role (e.g., "Admin", "Editor").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// An optional human-friendly display name for the role.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// An optional description explaining the role's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The sort order position for display purposes.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Whether the role is active and can be assigned to users.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
