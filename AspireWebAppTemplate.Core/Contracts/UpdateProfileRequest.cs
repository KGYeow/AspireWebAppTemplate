namespace AspireWebAppTemplate.Core.Contracts;

/// <summary>
/// Request body for the current user to update their own profile (phone number, etc.).
/// </summary>
public sealed class UpdateProfileRequest
{
    public string? DisplayName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
}
