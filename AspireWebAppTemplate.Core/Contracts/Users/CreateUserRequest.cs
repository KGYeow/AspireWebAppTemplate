namespace AspireWebAppTemplate.Core.Contracts.Users;

/// <summary>
/// Request payload for creating a new user account via the admin interface.
/// Sent from the admin UI to the Users API endpoint.
/// </summary>
public sealed class CreateUserRequest
{
    /// <summary>
    /// The user's email address (also used as their username).
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// The user's display name shown throughout the application.
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// The initial password for the new account.
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// The optional role to assign to the user upon creation.
    /// </summary>
    public string? Role { get; set; }
}
