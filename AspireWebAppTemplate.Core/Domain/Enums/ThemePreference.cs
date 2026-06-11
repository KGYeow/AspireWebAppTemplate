namespace AspireWebAppTemplate.Core.Domain.Enums;

/// <summary>
/// Represents the user's preferred UI theme.
/// Stored as a string in the database via EF Core HasConversion.
/// </summary>
public enum ThemePreference
{
    /// <summary>Follow the operating system / browser preference.</summary>
    System,

    /// <summary>Always use the light theme.</summary>
    Light,

    /// <summary>Always use the dark theme.</summary>
    Dark
}
