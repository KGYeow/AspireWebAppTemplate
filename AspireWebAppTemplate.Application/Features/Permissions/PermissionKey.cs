using System.Text.RegularExpressions;

namespace AspireWebAppTemplate.Application.Features.Permissions;

/// <summary>
/// Provides validation and parsing helpers for permission key strings in the
/// <c>Module.Action</c> format (e.g. <c>Users.Read</c>, <c>AuditLog.Export</c>).
/// </summary>
/// <remarks>
/// A valid permission key consists of exactly one dot separating two PascalCase segments,
/// each between 1 and 50 characters. Each segment begins with an uppercase letter followed
/// by up to 49 alphanumeric characters, enforced by the pattern
/// <c>^[A-Z][a-zA-Z0-9]{0,49}\.[A-Z][a-zA-Z0-9]{0,49}$</c>. This is pure, dependency-free
/// logic in the Application layer, shared by the permission service, seed data, and the
/// authorization matrix UI.
/// </remarks>
public static partial class PermissionKey
{
    #region Validation

    /// <summary>
    /// Compiled regex enforcing the <c>Module.Action</c> permission key format: exactly one dot
    /// separating two PascalCase segments, each 1–50 characters (uppercase first letter followed
    /// by up to 49 alphanumeric characters).
    /// </summary>
    [GeneratedRegex(@"^[A-Z][a-zA-Z0-9]{0,49}\.[A-Z][a-zA-Z0-9]{0,49}$", RegexOptions.Compiled)]
    private static partial Regex KeyFormatRegex();

    /// <summary>
    /// Determines whether the specified string is a valid permission key in the
    /// <c>Module.Action</c> format.
    /// </summary>
    /// <param name="key">The candidate permission key to validate.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="key"/> matches the required
    /// <c>Module.Action</c> PascalCase pattern; otherwise, <see langword="false"/>
    /// (including when <paramref name="key"/> is <see langword="null"/> or empty).
    /// </returns>
    public static bool IsValid(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        return KeyFormatRegex().IsMatch(key);
    }

    #endregion

    #region Parsing

    /// <summary>
    /// Extracts the Module segment (the substring before the dot) from a valid permission key.
    /// </summary>
    /// <param name="key">The permission key to parse.</param>
    /// <returns>
    /// The Module segment of <paramref name="key"/> when it is a valid permission key;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    public static string? GetModule(string key)
    {
        if (!IsValid(key))
        {
            return null;
        }

        // A valid key always contains exactly one dot, so the module is the substring before it.
        return key[..key.IndexOf('.')];
    }

    #endregion
}
