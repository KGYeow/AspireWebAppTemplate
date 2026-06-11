namespace AspireWebAppTemplate.Core.Contracts;

/// <summary>
/// Request body for the current user to update their own profile (phone number, etc.).
/// </summary>
public sealed class UpdateProfileRequest
{
    public string? PhoneNumber { get; set; }
}
