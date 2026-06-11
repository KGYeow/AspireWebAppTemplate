namespace AspireWebAppTemplate.Core.Contracts;

public sealed class CreateUserRequest
{
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Role { get; set; }
}
