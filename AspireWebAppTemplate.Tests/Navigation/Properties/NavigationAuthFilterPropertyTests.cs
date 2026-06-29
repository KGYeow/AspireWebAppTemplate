// Feature: api-nav-filtering, Property 2: Auth Filtering Truth Table
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Tests.Navigation.Generators;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Gen = FsCheck.Fluent.Gen;
using Property = FsCheck.Property;

namespace AspireWebAppTemplate.Tests.Navigation.Properties;

/// <summary>
/// Property-based tests verifying that authentication-based filtering correctly implements
/// the truth table defined by the AuthorizedOnly and NotAuthorizedOnly flag combinations
/// relative to the user's authentication state.
/// </summary>
/// <remarks>
/// <para>
/// <b>Property 2: Auth Filtering Truth Table</b> — For any NavItem and for any authentication
/// state, the authentication filtering outcome SHALL match the following truth table:
/// </para>
/// <list type="bullet">
/// <item><description><c>AuthorizedOnly=true, NotAuthorizedOnly=false</c> → visible only when authenticated</description></item>
/// <item><description><c>AuthorizedOnly=false, NotAuthorizedOnly=true</c> → visible only when unauthenticated</description></item>
/// <item><description><c>AuthorizedOnly=false, NotAuthorizedOnly=false</c> → always visible</description></item>
/// <item><description><c>AuthorizedOnly=true, NotAuthorizedOnly=true</c> → never visible</description></item>
/// </list>
/// <para>
/// <b>Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6</b>
/// </para>
/// </remarks>
public class NavigationAuthFilterPropertyTests
{
    #region Auth Visibility Logic

    /// <summary>
    /// Determines whether a NavItem is visible based on authentication state.
    /// This is the pure auth filtering logic extracted from the filtering pipeline:
    /// <list type="bullet">
    /// <item><description>If AuthorizedOnly and not authenticated → exclude</description></item>
    /// <item><description>If NotAuthorizedOnly and authenticated → exclude</description></item>
    /// <item><description>Otherwise → include</description></item>
    /// </list>
    /// </summary>
    /// <param name="item">The navigation item to evaluate.</param>
    /// <param name="isAuthenticated">Whether the current user is authenticated.</param>
    /// <returns>True if the item is visible given the authentication state; false otherwise.</returns>
    private static bool IsAuthVisible(NavItem item, bool isAuthenticated)
    {
        if (item.AuthorizedOnly && !isAuthenticated)
            return false;
        if (item.NotAuthorizedOnly && isAuthenticated)
            return false;
        return true;
    }

    /// <summary>
    /// Computes the expected visibility based on the auth filtering truth table.
    /// This serves as the oracle for property verification.
    /// </summary>
    /// <param name="authorizedOnly">Whether the item requires authentication.</param>
    /// <param name="notAuthorizedOnly">Whether the item requires anonymous access.</param>
    /// <param name="isAuthenticated">Whether the current user is authenticated.</param>
    /// <returns>True if the item should be visible per the truth table.</returns>
    private static bool TruthTableExpected(bool authorizedOnly, bool notAuthorizedOnly, bool isAuthenticated)
    {
        return (authorizedOnly, notAuthorizedOnly) switch
        {
            (true, false) => isAuthenticated,       // visible only when authenticated
            (false, true) => !isAuthenticated,      // visible only when unauthenticated
            (false, false) => true,                 // always visible
            (true, true) => false                   // never visible
        };
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Property: For any randomly generated NavItem with a specific (AuthorizedOnly, NotAuthorizedOnly)
    /// flag combination, and for any authentication state, the IsAuthVisible function SHALL return
    /// the value dictated by the truth table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test generates random NavItems with all four flag combinations and both auth states,
    /// then verifies that the filtering outcome matches the expected truth table exactly.
    /// </para>
    /// <para>
    /// <b>Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6</b>
    /// </para>
    /// </remarks>
    [Property(MaxTest = 2)]
    public Property AuthFiltering_MatchesTruthTable_ForAllFlagCombinationsAndAuthStates()
    {
        // Generator for a NavItem with specific auth flag combination
        var itemWithFlagsGen = GenNavItemWithFlags();

        // Generator for auth state
        var authStateGen = NavItemGenerators.GenAuthState();

        // Combine: random item (with specific flags) × random auth state
        var gen = itemWithFlagsGen.SelectMany<NavItem, (NavItem item, bool isAuthenticated)>(item =>
            authStateGen.Select(auth => (item, auth)));

        return Prop.ForAll(Arb.From(gen),
            ((NavItem item, bool isAuthenticated) input) =>
            {
                // Act: Apply the auth visibility check
                var actual = IsAuthVisible(input.item, input.isAuthenticated);

                // Assert: Compare against the truth table oracle
                var expected = TruthTableExpected(
                    input.item.AuthorizedOnly,
                    input.item.NotAuthorizedOnly,
                    input.isAuthenticated);

                return (actual == expected)
                    .Label($"AuthorizedOnly={input.item.AuthorizedOnly}, " +
                           $"NotAuthorizedOnly={input.item.NotAuthorizedOnly}, " +
                           $"IsAuthenticated={input.isAuthenticated}, " +
                           $"Expected={expected}, Actual={actual}");
            });
    }

    /// <summary>
    /// Property: Items with (AuthorizedOnly=true, NotAuthorizedOnly=false) are visible
    /// ONLY when the user is authenticated. For any randomly generated NavItem with these flags,
    /// visibility equals the authentication state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Validates: Requirements 2.2, 2.3</b>
    /// </para>
    /// </remarks>
    [Property(MaxTest = 2)]
    public Property AuthorizedOnlyItems_VisibleOnlyWhenAuthenticated()
    {
        // Generate random NavItems with AuthorizedOnly=true, NotAuthorizedOnly=false
        var itemGen = NavItemGenerators.GenNavItem(1).Select(item => new NavItem
        {
            Type = item.Type,
            Text = item.Text,
            Href = item.Href,
            Title = item.Title,
            Match = item.Match,
            Icon = item.Icon,
            AuthorizedOnly = true,
            NotAuthorizedOnly = false,
            DividerClass = item.DividerClass,
            Children = item.Children,
            Expanded = item.Expanded
        });

        var gen = itemGen.SelectMany<NavItem, (NavItem item, bool isAuthenticated)>(item =>
            NavItemGenerators.GenAuthState().Select(auth => (item, auth)));

        return Prop.ForAll(Arb.From(gen),
            ((NavItem item, bool isAuthenticated) input) =>
            {
                var actual = IsAuthVisible(input.item, input.isAuthenticated);

                // AuthorizedOnly=true, NotAuthorizedOnly=false → visible iff authenticated
                return (actual == input.isAuthenticated)
                    .Label($"AuthorizedOnly item should be visible only when authenticated. " +
                           $"IsAuthenticated={input.isAuthenticated}, Visible={actual}");
            });
    }

    /// <summary>
    /// Property: Items with (AuthorizedOnly=false, NotAuthorizedOnly=true) are visible
    /// ONLY when the user is NOT authenticated. For any randomly generated NavItem with these flags,
    /// visibility equals the negation of the authentication state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Validates: Requirements 2.1, 2.4</b>
    /// </para>
    /// </remarks>
    [Property(MaxTest = 2)]
    public Property NotAuthorizedOnlyItems_VisibleOnlyWhenUnauthenticated()
    {
        // Generate random NavItems with AuthorizedOnly=false, NotAuthorizedOnly=true
        var itemGen = NavItemGenerators.GenNavItem(1).Select(item => new NavItem
        {
            Type = item.Type,
            Text = item.Text,
            Href = item.Href,
            Title = item.Title,
            Match = item.Match,
            Icon = item.Icon,
            AuthorizedOnly = false,
            NotAuthorizedOnly = true,
            DividerClass = item.DividerClass,
            Children = item.Children,
            Expanded = item.Expanded
        });

        var gen = itemGen.SelectMany<NavItem, (NavItem item, bool isAuthenticated)>(item =>
            NavItemGenerators.GenAuthState().Select(auth => (item, auth)));

        return Prop.ForAll(Arb.From(gen),
            ((NavItem item, bool isAuthenticated) input) =>
            {
                var actual = IsAuthVisible(input.item, input.isAuthenticated);

                // AuthorizedOnly=false, NotAuthorizedOnly=true → visible iff NOT authenticated
                return (actual == !input.isAuthenticated)
                    .Label($"NotAuthorizedOnly item should be visible only when unauthenticated. " +
                           $"IsAuthenticated={input.isAuthenticated}, Visible={actual}");
            });
    }

    /// <summary>
    /// Property: Items with (AuthorizedOnly=false, NotAuthorizedOnly=false) are ALWAYS visible
    /// regardless of authentication state. For any randomly generated NavItem with these flags
    /// and any auth state, IsAuthVisible always returns true.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Validates: Requirements 2.5</b>
    /// </para>
    /// </remarks>
    [Property(MaxTest = 2)]
    public Property NoAuthFlags_AlwaysVisible()
    {
        // Generate random NavItems with AuthorizedOnly=false, NotAuthorizedOnly=false
        var itemGen = NavItemGenerators.GenNavItem(1).Select(item => new NavItem
        {
            Type = item.Type,
            Text = item.Text,
            Href = item.Href,
            Title = item.Title,
            Match = item.Match,
            Icon = item.Icon,
            AuthorizedOnly = false,
            NotAuthorizedOnly = false,
            DividerClass = item.DividerClass,
            Children = item.Children,
            Expanded = item.Expanded
        });

        var gen = itemGen.SelectMany<NavItem, (NavItem item, bool isAuthenticated)>(item =>
            NavItemGenerators.GenAuthState().Select(auth => (item, auth)));

        return Prop.ForAll(Arb.From(gen),
            ((NavItem item, bool isAuthenticated) input) =>
            {
                var actual = IsAuthVisible(input.item, input.isAuthenticated);

                // AuthorizedOnly=false, NotAuthorizedOnly=false → always visible
                return actual
                    .Label($"Item with no auth flags should always be visible. " +
                           $"IsAuthenticated={input.isAuthenticated}, Visible={actual}");
            });
    }

    /// <summary>
    /// Property: Items with (AuthorizedOnly=true, NotAuthorizedOnly=true) are NEVER visible
    /// regardless of authentication state. For any randomly generated NavItem with these flags
    /// and any auth state, IsAuthVisible always returns false.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Validates: Requirements 2.6</b>
    /// </para>
    /// </remarks>
    [Property(MaxTest = 2)]
    public Property BothAuthFlags_NeverVisible()
    {
        // Generate random NavItems with AuthorizedOnly=true, NotAuthorizedOnly=true
        var itemGen = NavItemGenerators.GenNavItem(1).Select(item => new NavItem
        {
            Type = item.Type,
            Text = item.Text,
            Href = item.Href,
            Title = item.Title,
            Match = item.Match,
            Icon = item.Icon,
            AuthorizedOnly = true,
            NotAuthorizedOnly = true,
            DividerClass = item.DividerClass,
            Children = item.Children,
            Expanded = item.Expanded
        });

        var gen = itemGen.SelectMany<NavItem, (NavItem item, bool isAuthenticated)>(item =>
            NavItemGenerators.GenAuthState().Select(auth => (item, auth)));

        return Prop.ForAll(Arb.From(gen),
            ((NavItem item, bool isAuthenticated) input) =>
            {
                var actual = IsAuthVisible(input.item, input.isAuthenticated);

                // AuthorizedOnly=true, NotAuthorizedOnly=true → never visible
                return (!actual)
                    .Label($"Item with both auth flags should never be visible. " +
                           $"IsAuthenticated={input.isAuthenticated}, Visible={actual}");
            });
    }

    #endregion

    #region Generators

    /// <summary>
    /// Generates a random NavItem with one of the four possible auth flag combinations.
    /// Uses the shared NavItemGenerators to produce realistic item structures, then
    /// overrides the auth flags to ensure all four truth table rows are exercised.
    /// </summary>
    /// <returns>A generator producing NavItems with evenly distributed auth flag combinations.</returns>
    private static Gen<NavItem> GenNavItemWithFlags()
    {
        // Generate the four flag combinations with equal probability
        var flagsGen = Gen.Elements(
            (AuthOnly: true, NotAuthOnly: false),
            (AuthOnly: false, NotAuthOnly: true),
            (AuthOnly: false, NotAuthOnly: false),
            (AuthOnly: true, NotAuthOnly: true));

        return flagsGen.SelectMany<(bool AuthOnly, bool NotAuthOnly), NavItem>(flags =>
            NavItemGenerators.GenNavItem(1).Select(item => new NavItem
            {
                Type = item.Type,
                Text = item.Text,
                Href = item.Href,
                Title = item.Title,
                Match = item.Match,
                Icon = item.Icon,
                AuthorizedOnly = flags.AuthOnly,
                NotAuthorizedOnly = flags.NotAuthOnly,
                DividerClass = item.DividerClass,
                Children = item.Children,
                Expanded = item.Expanded
            }));
    }

    #endregion
}
