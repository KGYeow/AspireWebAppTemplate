namespace AspireWebAppTemplate.Core.Contracts.Auth;

/// <summary>
/// Request payload for validating a single-use login token.
/// Used by the PerformLogin endpoint to redeem a token for a cookie.
/// </summary>
public sealed class ValidateTokenRequest
{
    /// <summary>
    /// The single-use login token to validate and consume.
    /// </summary>
    public string Token { get; set; } = "";
}
