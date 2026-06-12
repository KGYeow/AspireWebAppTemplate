namespace AspireWebAppTemplate.Core.Contracts.Auth;

public sealed class ConfirmEmailRequest
{
    public string UserId { get; set; } = "";
    public string Code { get; set; } = "";
}
