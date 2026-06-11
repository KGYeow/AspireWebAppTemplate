namespace AspireWebAppTemplate.Core.Contracts;

/// <summary>
/// Represents the outcome of a user registration attempt.
/// </summary>
public sealed class RegisterResult
{
    /// <summary>
    /// Indicates whether the user was successfully created.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Indicates whether the application requires email confirmation before sign-in.
    /// </summary>
    public bool RequiresEmailConfirmation { get; init; }

    /// <summary>
    /// The registered user's email address. Used by the component for redirect parameters.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Combined error descriptions when registration fails.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
