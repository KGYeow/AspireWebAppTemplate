namespace AspireWebAppTemplate.Core.Contracts.Auth;

public sealed class ResetPasswordRequest
{
    public string Email { get; set; } = "";
    public string Code { get; set; } = "";
    public string NewPassword { get; set; } = "";
}
