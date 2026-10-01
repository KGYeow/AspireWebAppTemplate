// Feature: resource-based-authorization, Property 2: Permission key format validation
using System.Text.RegularExpressions;
using AspireWebAppTemplate.Application.Features.Permissions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspireWebAppTemplate.Tests.ResourceBasedAuthorization;

/// <summary>
/// Property-based tests verifying that <see cref="PermissionKey.IsValid(string)"/> accepts a
/// string if and only if it matches the <c>Module.Action</c> permission key format — exactly one
/// dot separating two PascalCase segments, each 1–50 characters — and that
/// <see cref="PermissionKey.GetModule(string)"/> returns the first (module) segment for valid keys.
/// </summary>
/// <remarks>
/// **Validates: Requirements 1.5, 1.6**
/// Property 2: the validator accepts a string iff it matches
/// <c>^[A-Z][a-zA-Z0-9]{0,49}\.[A-Z][a-zA-Z0-9]{0,49}$</c>; all other strings are rejected.
/// Valid keys are generated constructively; invalid keys are drawn both from arbitrary strings and
/// from targeted malformed shapes (no dot, multiple dots, lowercase starts, empty/over-long
/// segments). The canonical regex serves as the independent oracle in every case.
/// </remarks>
public class PermissionKeyValidationTests
{
    #region Oracle

    /// <summary>
    /// Independent reference implementation of the permission key pattern used as the test oracle.
    /// Mirrors the production regex so the property compares the validator against the spec pattern
    /// rather than against itself.
    /// </summary>
    private static readonly Regex ReferenceRegex =
        new(@"^[A-Z][a-zA-Z0-9]{0,49}\.[A-Z][a-zA-Z0-9]{0,49}$", RegexOptions.Compiled);

    #endregion

    #region Generators

    /// <summary>
    /// Generates a single valid PascalCase segment: an uppercase first letter followed by 0–49
    /// alphanumeric characters, yielding a total length of 1–50 characters.
    /// </summary>
    private static Gen<string> ValidSegmentGen()
    {
        var upper = Gen.Elements("ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray());
        var alnum = Gen.Elements(
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789".ToCharArray());

        return upper.SelectMany(first =>
            Gen.Choose(0, 49).SelectMany(tailLen =>
                Gen.ArrayOf(alnum, tailLen)
                    .Select(tail => first + new string(tail))));
    }

    /// <summary>
    /// Generates a valid permission key by joining two valid PascalCase segments with a single dot.
    /// </summary>
    private static Gen<string> ValidKeyGen()
    {
        return ValidSegmentGen().SelectMany(module =>
            ValidSegmentGen().Select(action => $"{module}.{action}"));
    }

    /// <summary>
    /// Generates strings that are intentionally malformed in ways the validator must reject:
    /// no dot, multiple dots, lowercase-starting segments, empty segments, and over-long segments.
    /// </summary>
    private static Gen<string> MalformedKeyGen()
    {
        return Gen.OneOf(
            // No dot at all.
            Gen.Elements("Users", "UsersRead", "AuditLog", "Permissions"),
            // Multiple dots (three segments).
            Gen.Elements("Users.Read.Extra", "A.B.C", "Module.Sub.Action"),
            // Lowercase first letter in one or both segments.
            Gen.Elements("users.Read", "Users.read", "users.read", "u.R", "U.r"),
            // Empty segment on one side.
            Gen.Elements(".Read", "Users.", ".", "..", "Users..Read"),
            // Over-long segment (> 50 chars) on either side.
            Gen.Elements(
                new string('A', 51) + ".Read",
                "Users." + new string('A', 51),
                new string('A', 60) + "." + new string('B', 60)),
            // Illegal characters.
            Gen.Elements("Users.Read!", "Users .Read", "User-s.Read", "Users.Re ad", "Users.Re@d"));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Property: every constructively generated valid key is accepted by the validator, agrees with
    /// the reference regex, and reports a module segment equal to the substring before the dot.
    /// **Validates: Requirements 1.5, 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public FsCheck.Property ValidKeys_AreAccepted_AndModuleIsFirstSegment()
    {
        return Prop.ForAll(
            Arb.From(ValidKeyGen()),
            (string key) =>
            {
                var expectedModule = key[..key.IndexOf('.')];

                var isValid = PermissionKey.IsValid(key);
                var matchesReference = ReferenceRegex.IsMatch(key);
                var module = PermissionKey.GetModule(key);

                return (isValid && matchesReference && module == expectedModule).Label(
                    $"Key '{key}': IsValid={isValid} (regex={matchesReference}), " +
                    $"GetModule='{module}' (expected '{expectedModule}')");
            });
    }

    /// <summary>
    /// Property: for arbitrary strings, the validator's verdict exactly matches the reference regex,
    /// and <see cref="PermissionKey.GetModule(string)"/> returns <see langword="null"/> for anything
    /// rejected. This is the "if and only if" half, exercising the full string space.
    /// **Validates: Requirements 1.5, 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public FsCheck.Property ArbitraryStrings_MatchReferenceRegex(string? input)
    {
        var key = input ?? string.Empty;

        var isValid = PermissionKey.IsValid(key);
        var matchesReference = ReferenceRegex.IsMatch(key);
        var module = PermissionKey.GetModule(key);

        var verdictAgrees = isValid == matchesReference;
        var moduleAgrees = isValid ? module == key[..key.IndexOf('.')] : module is null;

        return (verdictAgrees && moduleAgrees).Label(
            $"Input '{key}': IsValid={isValid}, regex={matchesReference}, GetModule='{module}'");
    }

    /// <summary>
    /// Property: targeted malformed keys (no dot, multiple dots, lowercase starts, empty or over-long
    /// segments, illegal characters) are always rejected, and their module is <see langword="null"/>.
    /// **Validates: Requirements 1.5, 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public FsCheck.Property MalformedKeys_AreRejected()
    {
        return Prop.ForAll(
            Arb.From(MalformedKeyGen()),
            (string key) =>
            {
                var isValid = PermissionKey.IsValid(key);
                var module = PermissionKey.GetModule(key);

                // Sanity-check the oracle agrees these shapes are invalid.
                var referenceRejects = !ReferenceRegex.IsMatch(key);

                return (!isValid && module is null && referenceRejects).Label(
                    $"Malformed key '{key}': IsValid={isValid}, GetModule='{module}', " +
                    $"referenceRejects={referenceRejects}");
            });
    }

    #endregion
}
