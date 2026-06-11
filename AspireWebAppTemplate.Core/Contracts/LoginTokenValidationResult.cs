namespace AspireWebAppTemplate.Core.Contracts;

public sealed class LoginTokenValidationResult
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public List<string> Roles { get; set; } = [];
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
