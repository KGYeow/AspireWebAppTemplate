// Feature: api-nav-filtering, Property 6: Path Normalization Idempotence and Correctness
using AspireWebAppTemplate.Tests.Navigation.Generators;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspireWebAppTemplate.Tests.Navigation.Properties;

/// <summary>
/// Property-based tests verifying that path normalization is idempotent and correctly
/// prepends leading slashes, strips trailing slashes, and supports case-insensitive
/// comparison for equivalent paths.
/// </summary>
/// <remarks>
/// <para>
/// <b>Validates: Requirements 8.1, 8.2, 8.4, 8.5</b>
/// </para>
/// <para>
/// Path normalization ensures consistent comparison between NavItem Href values and
/// page permission paths. The normalization rules are:
/// <list type="bullet">
///   <item><description>null → returns null (skip comparison)</description></item>
///   <item><description>empty string → "/"</description></item>
///   <item><description>No leading "/" → prepend "/"</description></item>
///   <item><description>Has trailing "/" (and path length > 1) → strip it</description></item>
///   <item><description>Comparison uses OrdinalIgnoreCase</description></item>
/// </list>
/// </para>
/// <para>
/// Properties verified:
/// <list type="bullet">
///   <item><description><b>Idempotence</b>: NormalizePath(NormalizePath(x)) == NormalizePath(x)</description></item>
///   <item><description><b>Leading slash guarantee</b>: result always starts with "/" for non-null input</description></item>
///   <item><description><b>No trailing slash</b>: result never ends with "/" except for root "/"</description></item>
///   <item><description><b>Case-insensitive equivalence</b>: paths differing only by case normalize to values equal under OrdinalIgnoreCase</description></item>
/// </list>
/// </para>
/// </remarks>
public class NavigationPathNormalizationPropertyTests
{
    #region Property Tests

    /// <summary>
    /// Property: Applying NormalizePath twice produces the same result as applying it once.
    /// This proves the normalization function is idempotent — once a path is normalized,
    /// further normalization has no effect.
    /// <para><b>Validates: Requirements 8.1, 8.4</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property NormalizePath_IsIdempotent()
    {
        var hrefGen = NavItemGenerators.GenHref();

        return Prop.ForAll(Arb.From(hrefGen), (string? href) =>
        {
            var once = NavigationFilteringHelper.NormalizePath(href);
            var twice = NavigationFilteringHelper.NormalizePath(once);

            var isIdempotent = once == twice;

            return isIdempotent
                .Label($"href=\"{href ?? "null"}\", once=\"{once ?? "null"}\", twice=\"{twice ?? "null"}\"");
        });
    }

    /// <summary>
    /// Property: For any non-null href input, the normalized result always starts with "/".
    /// This guarantees consistent path format for permission comparisons.
    /// <para><b>Validates: Requirements 8.1, 8.2</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property NormalizePath_NonNull_AlwaysStartsWithSlash()
    {
        // Filter out null values — we only test non-null inputs for this property
        var nonNullHrefGen = NavItemGenerators.GenHref()
            .Where(h => h is not null)
            .Select(h => h!);

        return Prop.ForAll(Arb.From(nonNullHrefGen), (string href) =>
        {
            var result = NavigationFilteringHelper.NormalizePath(href);

            var startsWithSlash = result!.StartsWith('/');

            return startsWithSlash
                .Label($"href=\"{href}\", result=\"{result}\"");
        });
    }

    /// <summary>
    /// Property: For any non-null href input, the normalized result never ends with "/"
    /// unless the result is exactly "/" (the root path). This ensures trailing slashes
    /// are stripped for consistent permission matching.
    /// <para><b>Validates: Requirements 8.4</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property NormalizePath_NonNull_NoTrailingSlash_ExceptRoot()
    {
        var nonNullHrefGen = NavItemGenerators.GenHref()
            .Where(h => h is not null)
            .Select(h => h!);

        return Prop.ForAll(Arb.From(nonNullHrefGen), (string href) =>
        {
            var result = NavigationFilteringHelper.NormalizePath(href);

            // Root "/" is the only valid result that ends with "/"
            var noTrailingSlash = result == "/" || !result!.EndsWith('/');

            return noTrailingSlash
                .Label($"href=\"{href}\", result=\"{result}\"");
        });
    }

    /// <summary>
    /// Property: Paths that differ only by letter case normalize to values that compare
    /// equal using OrdinalIgnoreCase. This validates that the normalization function
    /// produces canonical forms compatible with case-insensitive permission matching.
    /// <para><b>Validates: Requirements 8.5</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property NormalizePath_CaseVariants_CompareEqualOrdinalIgnoreCase()
    {
        // Generate non-null hrefs and compare their normalization with an uppercased version
        var nonNullHrefGen = NavItemGenerators.GenHref()
            .Where(h => h is not null && h.Length > 0)
            .Select(h => h!);

        return Prop.ForAll(Arb.From(nonNullHrefGen), (string href) =>
        {
            var normalizedOriginal = NavigationFilteringHelper.NormalizePath(href);
            var normalizedUpper = NavigationFilteringHelper.NormalizePath(href.ToUpperInvariant());
            var normalizedLower = NavigationFilteringHelper.NormalizePath(href.ToLowerInvariant());

            var originalEqualsUpper = string.Equals(normalizedOriginal, normalizedUpper, StringComparison.OrdinalIgnoreCase);
            var originalEqualsLower = string.Equals(normalizedOriginal, normalizedLower, StringComparison.OrdinalIgnoreCase);

            return (originalEqualsUpper && originalEqualsLower)
                .Label($"href=\"{href}\", normalized=\"{normalizedOriginal}\", " +
                       $"upper=\"{normalizedUpper}\", lower=\"{normalizedLower}\"");
        });
    }

    #endregion
}
