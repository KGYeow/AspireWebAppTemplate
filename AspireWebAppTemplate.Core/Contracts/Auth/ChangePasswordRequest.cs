namespace AspireWebAppTemplate.Core.Contracts.Auth;

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}
