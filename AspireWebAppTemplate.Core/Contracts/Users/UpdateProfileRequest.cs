namespace AspireWebAppTemplate.Core.Contracts.Users;

/// <summary>
/// Request body for the current user to update their own profile information.
/// Sent from the profile settings page to the API.
/// </summary>
public sealed class UpdateProfileRequest
{
    /// <summary>
    /// The user's display name shown throughout the application.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// The user's first/given name.
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// The user's last/family name.
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// The user's phone number.
    /// </summary>
    public string? PhoneNumber { get; set; }
}
