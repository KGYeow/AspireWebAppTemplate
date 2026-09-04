namespace AspireWebAppTemplate.Application.Features.Template.Users;

/// <summary>
/// Request payload for an administrator to update another user's profile information.
/// Sent from the admin user management UI to the Users API endpoint.
/// </summary>
public sealed class UpdateUserRequest
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
    /// The user's email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// The user's phone number.
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// The user's job title.
    /// </summary>
    public string? JobTitle { get; set; }

    /// <summary>
    /// The user's department.
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// The user's employee number.
    /// </summary>
    public string? EmployeeNumber { get; set; }
}
