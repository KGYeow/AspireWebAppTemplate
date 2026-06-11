namespace AspireWebAppTemplate.Core.Contracts;

public sealed class ResetPasswordRequest
{
    public string Email { get; set; } = "";
    public string Code { get; set; } = "";
    public string NewPassword { get; set; } = "";
}
