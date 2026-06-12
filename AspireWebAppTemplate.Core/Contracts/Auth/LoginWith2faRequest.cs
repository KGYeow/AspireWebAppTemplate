namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Request payload for logging in with a two-factor authentication code.
/// </summary>
public sealed class LoginWith2faRequest
{
    public string Code { get; set; } = "";
    public bool RememberMe { get; set; }
    public bool RememberMachine { get; set; }
}
