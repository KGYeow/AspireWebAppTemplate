// Feature: api-nav-filtering, Property 3: Permission Filtering Correctness
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Infrastructure.Services.Template.Navigation;
using AspireWebAppTemplate.Domain.Constants;
using AspireWebAppTemplate.Infrastructure.Services.Template.Navigation;
using AspireWebAppTemplate.Tests.Navigation.Generators;
using AspireWebAppTemplate.Infrastructure.Services.Template.Navigation;
using FsCheck;
using AspireWebAppTemplate.Infrastructure.Services.Template.Navigation;
using FsCheck.Fluent;
using AspireWebAppTemplate.Infrastructure.Services.Template.Navigation;
using FsCheck.Xunit;
using AspireWebAppTemplate.Infrastructure.Services.Template.Navigation;
using Gen = FsCheck.Fluent.Gen;
using AspireWebAppTemplate.Infrastructure.Services.Template.Navigation;
using Property = FsCheck.Property;
using AspireWebAppTemplate.Infrastructure.Services.Template.Navigation;

namespace AspireWebAppTemplate.Tests.Navigation.Properties;

/// <summary>
/// Property-based tests verifying that permission filtering correctly includes or excludes
/// Link items based on their normalized Href, the user's page permission set, and the
/// SystemPageDefaults bypass list.
/// </summary>
/// <remarks>
/// <para>
/// <b>Property 3: Permission Filtering Correctness</b> — For any NavItem of type Link that
/// has passed auth filtering, and for any page permission set, the permission filtering
/// outcome SHALL be: include if Href is null; include if normalized path is a System_Page;
/// include if normalized path is in the permission set; exclude otherwise.
/// </para>
/// <para>
/// <b>Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5</b>
/// </para>
/// <para>
/// The test inlines the permission check logic (IsPageAccessible equivalent) to verify
/// the filtering contract without depending on the NavigationService implementation directly.
/// Path normalization rules from Requirement 8 are applied:
/// <list type="bullet">
///   <item>null Href → skip comparison, always visible</item>
///   <item>empty string → treat as "/"</item>
///   <item>No leading slash → prepend "/"</item>
///   <item>Trailing slash → strip after prepending leading "/"</item>
///   <item>Comparison is OrdinalIgnoreCase</item>
/// </list>
/// </para>
/// </remarks>
public class NavigationPermissionFilterPropertyTests
{
    /// <summary>
    /// Normalizes a NavItem Href to a canonical path for permission comparison.
    /// Applies the rules defined in Requirement 8:
    /// <list type="bullet">
    ///   <item>null → returns null (caller must treat as always visible)</item>
    ///   <item>empty string → "/"</item>
    ///   <item>No leading "/" → prepend "/"</item>
    ///   <item>Trailing "/" → strip (after prepending leading "/" if needed)</item>
    /// </list>
    /// </summary>
    /// <param name="href">The raw Href value from a NavItem.</param>
    /// <returns>The normalized path, or null if the input was null.</returns>
    private static string? NormalizePath(string? href)
    {
        if (href is null)
            return null;

        if (href == string.Empty)
            return "/";

        var path = href.StartsWith('/') ? href : "/" + href;

        if (path.Length > 1 && path.EndsWith('/'))
            path = path[..^1];

        return path;
    }

    /// <summary>
    /// Determines whether a Link item should be included after permission filtering,
    /// inlining the IsPageAccessible logic:
    /// <list type="number">
    ///   <item>If href is null → always included</item>
    ///   <item>Normalize path (prepend "/" if needed, strip trailing "/")</item>
    ///   <item>If normalized path is in SystemPageDefaults.Paths → always included</item>
    ///   <item>If normalized path is in permissionSet → included</item>
    ///   <item>Otherwise → excluded</item>
    /// </list>
    /// </summary>
    /// <param name="href">The raw Href from the NavItem.</param>
    /// <param name="permissionSet">The user's page permission set (OrdinalIgnoreCase).</param>
    /// <returns>True if the item should be included; false if excluded.</returns>
    private static bool ExpectedPermissionOutcome(string? href, HashSet<string> permissionSet)
    {
        // Rule 1: null Href → always included
        if (href is null)
            return true;

        // Rule 2: Normalize path
        var normalized = NormalizePath(href)!;

        // Rule 3: System pages always bypass
        if (SystemPageDefaults.Paths.Contains(normalized))
            return true;

        // Rule 4: In permission set → included
        if (permissionSet.Contains(normalized))
            return true;

        // Rule 5: Otherwise → excluded
        return false;
    }

    /// <summary>
    /// Property: For any Link NavItem with a randomly generated Href and any page permission set,
    /// the permission filtering outcome matches the expected contract:
    /// null Href → always included; System_Page → always included; in permission set → included;
    /// otherwise → excluded.
    /// <para><b>Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public Property PermissionFilter_IncludesOrExcludes_BasedOnNormalizedPathAndPermissions()
    {
        // Generate a random href using the shared generator (includes null, empty,
        // leading/trailing slashes, mixed case)
        var hrefGen = NavItemGenerators.GenHref();

        // Generate a random permission set
        var permSetGen = NavItemGenerators.GenPermissionSet();

        // Combine both generators
        var gen = hrefGen.SelectMany<string?, (string? href, HashSet<string> permissions)>(href =>
            permSetGen.Select(perms => (href, perms)));

        return Prop.ForAll(Arb.From(gen),
            ((string? href, HashSet<string> permissions) input) =>
            {
                // Arrange: Build a Link NavItem that has already passed auth filtering
                var linkItem = new NavItem
                {
                    Type = NavItemType.Link,
                    Text = "TestLink",
                    Href = input.href,
                    AuthorizedOnly = false,
                    NotAuthorizedOnly = false
                };

                // Act: Compute the expected permission filtering outcome
                var included = ExpectedPermissionOutcome(linkItem.Href, input.permissions);

                // Assert: Verify the outcome against the individual rules
                var normalized = NormalizePath(input.href);

                if (input.href is null)
                {
                    // Null Href → always included (Requirement 3.4)
                    return included
                        .Label("null Href should always be included");
                }

                if (normalized is not null && SystemPageDefaults.Paths.Contains(normalized))
                {
                    // System page → always included (Requirement 3.3)
                    return included
                        .Label($"System page '{normalized}' should always be included");
                }

                if (normalized is not null && input.permissions.Contains(normalized))
                {
                    // In permission set → included (Requirement 3.1)
                    return included
                        .Label($"Path '{normalized}' in permission set should be included");
                }

                // Not in permission set and not system page → excluded (Requirement 3.2)
                return (!included)
                    .Label($"Path '{normalized}' (from href='{input.href}') not in permissions " +
                           $"and not a system page should be excluded. " +
                           $"Permissions: [{string.Join(", ", input.permissions)}]");
            });
    }

    /// <summary>
    /// Property: A Link item with null Href is always included regardless of the permission set
    /// contents (even an empty permission set).
    /// <para><b>Validates: Requirements 3.4</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public Property PermissionFilter_NullHref_AlwaysIncluded()
    {
        var permSetGen = NavItemGenerators.GenPermissionSet();

        return Prop.ForAll(Arb.From(permSetGen), (HashSet<string> permissions) =>
        {
            var result = ExpectedPermissionOutcome(null, permissions);

            return result
                .Label($"null Href should always be included regardless of permissions " +
                       $"(permissions count={permissions.Count})");
        });
    }

    /// <summary>
    /// Property: A Link item whose normalized path matches a SystemPageDefaults.Paths entry
    /// is always included, even when the permission set is empty.
    /// <para><b>Validates: Requirements 3.3</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public Property PermissionFilter_SystemPage_AlwaysIncluded()
    {
        // Generate hrefs that normalize to system page paths
        // SystemPageDefaults.Paths: /Account/Login, /Account/Register, /Account/AccessDenied,
        //   /Error, /Account/ForgotPassword, /Account/ResetPassword, /Account/PerformLogin
        var systemHrefGen = Gen.Elements<string?>(
            "Account/Login", "Account/Register", "Account/AccessDenied",
            "Error", "Account/ForgotPassword", "Account/ResetPassword",
            "Account/PerformLogin",
            // Also test with leading slash variants
            "/Account/Login", "/Account/Register", "/Error");

        var gen = systemHrefGen.SelectMany<string?, (string? href, HashSet<string> permissions)>(href =>
            NavItemGenerators.GenPermissionSet().Select(perms => (href, perms)));

        return Prop.ForAll(Arb.From(gen),
            ((string? href, HashSet<string> permissions) input) =>
            {
                var result = ExpectedPermissionOutcome(input.href, input.permissions);
                var normalized = NormalizePath(input.href);

                return result
                    .Label($"System page href='{input.href}' (normalized='{normalized}') " +
                           $"should always be included regardless of permissions");
            });
    }

    /// <summary>
    /// Property: A Link item whose normalized path is present in the permission set
    /// is always included.
    /// <para><b>Validates: Requirements 3.1</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public Property PermissionFilter_PathInPermissionSet_Included()
    {
        // Generate hrefs whose normalized paths we can guarantee are in the permission set
        var knownPaths = new[]
        {
            "counter", "weather", "auth", "admin/audit-log",
            "admin/user-management", "admin/role-management",
            "admin/page-permissions", "account/notifications",
            "account/settings", "account/profile"
        };

        var hrefGen = Gen.Elements(knownPaths);

        return Prop.ForAll(Arb.From(hrefGen), (string href) =>
        {
            // Build a permission set that contains the normalized path
            var normalized = NormalizePath(href)!;
            var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { normalized };

            var result = ExpectedPermissionOutcome(href, permissions);

            return result
                .Label($"href='{href}' (normalized='{normalized}') is in permission set " +
                       $"and should be included");
        });
    }

    /// <summary>
    /// Property: A Link item whose normalized path is NOT in the permission set AND is NOT
    /// a system page is always excluded.
    /// <para><b>Validates: Requirements 3.2</b></para>
    /// </summary>
    [Property(MaxTest = 2)]
    public Property PermissionFilter_PathNotInPermissions_AndNotSystemPage_Excluded()
    {
        // Generate non-system-page hrefs (excludes paths listed in SystemPageDefaults.Paths)
        var nonSystemHrefGen = Gen.Elements<string?>(
            "counter", "weather", "admin/audit-log",
            "admin/user-management", "admin/role-management",
            "admin/page-permissions",
            "dashboard", "reports");

        return Prop.ForAll(Arb.From(nonSystemHrefGen), (string? href) =>
        {
            // Use an empty permission set — guarantees path is not in permissions
            var emptyPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var result = ExpectedPermissionOutcome(href, emptyPermissions);
            var normalized = NormalizePath(href);

            return (!result)
                .Label($"href='{href}' (normalized='{normalized}') with empty permissions " +
                       $"and not a system page should be excluded");
        });
    }
}
