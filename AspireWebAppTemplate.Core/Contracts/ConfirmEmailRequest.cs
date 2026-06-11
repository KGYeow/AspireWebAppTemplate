namespace AspireWebAppTemplate.Core.Contracts;

public sealed class ConfirmEmailRequest
{
    public string UserId { get; set; } = "";
    public string Code { get; set; } = "";
}
