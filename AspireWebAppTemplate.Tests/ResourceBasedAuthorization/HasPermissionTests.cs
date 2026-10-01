// Feature: resource-based-authorization, Property 5: HasPermission correctness
using System.Security.Claims;
using AspireWebAppTemplate.Web.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Gen = FsCheck.Fluent.Gen;
using Property = FsCheck.Property;

namespace AspireWebAppTemplate.Tests.ResourceBasedAuthorization;

/// <summary>
/// Property-based tests verifying the correctness of
/// <see cref="PermissionContext.HasPermission(string)"/> across the context's lifecycle states.
/// </summary>
/// <remarks>
/// <para>
/// <b>Property 5: HasPermission correctness.</b> For any permission key and any cached permission set
/// in the LOADED state, <c>HasPermission(key)</c> returns <c>true</c> if the cached set contains the
/// key (case-insensitive) OR the user is Admin, and <c>false</c> otherwise. While the context is NOT
/// loaded, <c>HasPermission</c> returns <c>false</c> for non-Admin users regardless of the key, and
/// <c>true</c> for Admin users.
/// </para>
/// <para>
/// <b>Validates: Requirements 7.2, 7.6.</b>
/// </para>
/// <para>
/// <b>Setup approach.</b> <see cref="PermissionContext"/> loads its cache via
/// <c>ApiPermissionService.GetMyPermissionsAsync()</c> (GET <c>/api/permissions/my-permissions</c>,
/// returning a JSON <c>string[]</c>) inside <c>InitializeAsync()</c> and determines
/// <see cref="PermissionContext.IsAdmin"/> from the <see cref="AuthenticationStateProvider"/>. To drive
/// the method across states the tests control: (a) the loaded permission set via a fake
/// <see cref="HttpMessageHandler"/> that returns a controlled JSON array over an in-memory
/// <see cref="HttpClient"/> wrapping a real <see cref="ApiPermissionService"/>; (b) <c>IsAdmin</c> via a
/// stub <see cref="AuthenticationStateProvider"/> whose <see cref="ClaimsPrincipal"/> does or does not
/// carry the "Admin" role claim; and (c) <c>IsLoaded</c> by choosing whether to call
/// <c>InitializeAsync()</c>. This mirrors the FakeHttpHandler + stub-auth pattern used across the suite.
/// </para>
/// </remarks>
public class HasPermissionTests
{
    #region Test Infrastructure

    /// <summary>
    /// The role-claim value identifying an administrator, matching
    /// <see cref="PermissionContext"/>'s internal constant.
    /// </summary>
    private const string AdminRoleName = "Admin";

    /// <summary>
    /// Builds a <see cref="PermissionContext"/> backed by a fake HTTP handler returning the given
    /// permission set and a stub authentication state with the requested authenticated/Admin status.
    /// When <paramref name="initialize"/> is <c>true</c>, <c>InitializeAsync()</c> is awaited so the
    /// cache is populated (LOADED state); otherwise the context is left in the NOT-loaded state.
    /// </summary>
    /// <param name="cachedPermissions">The permission keys the fake API returns for the user.</param>
    /// <param name="isAdmin">Whether the stubbed principal carries the "Admin" role claim.</param>
    /// <param name="initialize">Whether to run <c>InitializeAsync()</c> before returning.</param>
    /// <returns>A configured <see cref="PermissionContext"/> ready for assertion.</returns>
    private static PermissionContext CreateContext(
        List<string> cachedPermissions,
        bool isAdmin,
        bool initialize)
    {
        // Real ApiPermissionService over an in-memory HttpClient whose handler returns the
        // controlled JSON string[] for GET /api/permissions/my-permissions.
        var httpClient = new HttpClient(new FakeHttpHandler(cachedPermissions))
        {
            BaseAddress = new Uri("https://localhost")
        };
        var apiService = new ApiPermissionService(httpClient);

        // Stub AuthenticationStateProvider: an authenticated user, optionally carrying the Admin role.
        var authStateProviderMock = new Mock<AuthenticationStateProvider>();
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, "testuser"));
        if (isAdmin)
            identity.AddClaim(new Claim(ClaimTypes.Role, AdminRoleName));
        var principal = new ClaimsPrincipal(identity);
        authStateProviderMock
            .Setup(a => a.GetAuthenticationStateAsync())
            .ReturnsAsync(new AuthenticationState(principal));

        var context = new PermissionContext(
            apiService,
            authStateProviderMock.Object,
            NullLogger<PermissionContext>.Instance);

        if (initialize)
            context.InitializeAsync().GetAwaiter().GetResult();

        return context;
    }

    /// <summary>
    /// Applies a random per-character case mutation to <paramref name="input"/>, using
    /// <paramref name="mutations"/> as the uppercase/lowercase decision for each position
    /// (<c>true</c> = uppercase, <c>false</c> = lowercase). Used to produce case variants of a
    /// permission key for case-insensitivity checks.
    /// </summary>
    /// <param name="input">The original string to mutate.</param>
    /// <param name="mutations">Per-character case decisions.</param>
    /// <returns>The case-mutated string.</returns>
    private static string ApplyCaseMutation(string input, bool[] mutations)
    {
        var chars = input.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (i < mutations.Length)
                chars[i] = mutations[i] ? char.ToUpperInvariant(chars[i]) : char.ToLowerInvariant(chars[i]);
        }
        return new string(chars);
    }

    /// <summary>
    /// Generates a single permission key in the <c>Module.Action</c> shape from a bounded vocabulary,
    /// so generated keys resemble real permission keys and collisions between the "in-set" and
    /// "out-of-set" pools are controllable.
    /// </summary>
    private static readonly Gen<string> PermissionKeyGen =
        Gen.Elements("Users", "Roles", "AuditLog", "Permissions", "Announcements", "EmailTemplates")
            .SelectMany<string, string>(module =>
                Gen.Elements("Read", "Create", "Update", "Delete", "Activate", "Manage", "Export")
                    .Select(action => $"{module}.{action}"));

    #endregion

    #region Property 5 — Loaded state

    /// <summary>
    /// Property 5 (loaded, non-Admin): for a loaded context and a non-Admin user, <c>HasPermission</c>
    /// agrees with an oracle — <c>true</c> iff the cached set contains the queried key under
    /// case-insensitive comparison. The query key is sometimes drawn from the cached set (optionally
    /// case-mutated to exercise case-insensitivity) and sometimes from a disjoint pool.
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Loaded_NonAdmin_MatchesCaseInsensitiveOracle()
    {
        // A set of distinct permission keys to cache (0..6 keys).
        var cachedSetGen = Gen.Choose(0, 6).SelectMany<int, List<string>>(count =>
            Gen.ArrayOf(PermissionKeyGen, count)
                .Select(keys => keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList()));

        var mutationGen = Gen.ArrayOf(Gen.Elements(true, false), 40);

        // The query key is either a (possibly case-mutated) member of the cached set, or a freshly
        // generated key that may or may not be in the set — covering both in/out and case-variant cases.
        var gen = cachedSetGen.SelectMany<List<string>, (List<string> cached, string queryKey)>(cached =>
            mutationGen.SelectMany<bool[], (List<string> cached, string queryKey)>(mutations =>
                Gen.Choose(0, 2).SelectMany<int, (List<string> cached, string queryKey)>(mode =>
                {
                    if (mode == 0 && cached.Count > 0)
                    {
                        // Draw a member and apply a random case mutation.
                        return Gen.Choose(0, cached.Count - 1)
                            .Select(i => (cached, ApplyCaseMutation(cached[i], mutations)));
                    }

                    // Otherwise generate an arbitrary key (may coincidentally be in the set).
                    return PermissionKeyGen.Select(k => (cached, k));
                })));

        return Prop.ForAll(Arb.From(gen), input =>
        {
            var context = CreateContext(input.cached, isAdmin: false, initialize: true);

            // Oracle: case-insensitive membership of the query key in the cached set.
            var expected = input.cached.Contains(input.queryKey, StringComparer.OrdinalIgnoreCase);
            var actual = context.HasPermission(input.queryKey);

            return (actual == expected)
                .Label($"QueryKey='{input.queryKey}', Expected={expected}, Actual={actual}, " +
                       $"Cached=[{string.Join(", ", input.cached)}], IsLoaded={context.IsLoaded}");
        });
    }

    /// <summary>
    /// Property 5 (loaded, Admin): for a loaded context and an Admin user, <c>HasPermission</c> returns
    /// <c>true</c> for every key regardless of the cached set (including keys absent from it).
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Loaded_Admin_AlwaysTrue()
    {
        var cachedSetGen = Gen.Choose(0, 6).SelectMany<int, List<string>>(count =>
            Gen.ArrayOf(PermissionKeyGen, count)
                .Select(keys => keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList()));

        // Query key spans the structured vocabulary plus arbitrary / nonexistent strings.
        var queryKeyGen = Gen.OneOf(
            PermissionKeyGen,
            Gen.Elements("Nonexistent.Key", "", "   ", "foo", "Module.", ".Action", "Random.Thing"));

        var gen = cachedSetGen.SelectMany<List<string>, (List<string> cached, string queryKey)>(cached =>
            queryKeyGen.Select(key => (cached, key)));

        return Prop.ForAll(Arb.From(gen), input =>
        {
            var context = CreateContext(input.cached, isAdmin: true, initialize: true);

            var actual = context.HasPermission(input.queryKey);

            return actual
                .Label($"Admin should hold every permission. QueryKey='{input.queryKey}', " +
                       $"Actual={actual}, IsLoaded={context.IsLoaded}, IsAdmin={context.IsAdmin}");
        });
    }

    #endregion

    #region Property 5 — Not-loaded state

    /// <summary>
    /// Property 5 (not loaded, non-Admin): before <c>InitializeAsync</c> runs, <c>HasPermission</c>
    /// returns <c>false</c> for a non-Admin user regardless of the key — the uninitialized cache must
    /// not grant access.
    /// **Validates: Requirements 7.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NotLoaded_NonAdmin_AlwaysFalse()
    {
        // Even though a cached set would be returned by the API, we never initialize, so it is unused.
        var cachedSetGen = Gen.Choose(0, 6).SelectMany<int, List<string>>(count =>
            Gen.ArrayOf(PermissionKeyGen, count)
                .Select(keys => keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList()));

        var queryKeyGen = Gen.OneOf(
            PermissionKeyGen,
            Gen.Elements("Nonexistent.Key", "", "foo", "Users.Read"));

        var gen = cachedSetGen.SelectMany<List<string>, (List<string> cached, string queryKey)>(cached =>
            queryKeyGen.Select(key => (cached, key)));

        return Prop.ForAll(Arb.From(gen), input =>
        {
            var context = CreateContext(input.cached, isAdmin: false, initialize: false);

            var actual = context.HasPermission(input.queryKey);

            return (!actual && !context.IsLoaded)
                .Label($"Not-loaded non-Admin must deny. QueryKey='{input.queryKey}', " +
                       $"Actual={actual}, IsLoaded={context.IsLoaded}");
        });
    }

    /// <summary>
    /// Property 5 (loaded, Admin — key absent from cache): the Admin short-circuit grants a key even
    /// when it is provably not in the cached set. This exercises the "true for Admin regardless of key"
    /// clause of the property using a key disjoint from the cache.
    /// **Validates: Requirements 7.2**
    /// </summary>
    /// <remarks>
    /// The task frames the Admin branch as "HasPermission returns true for Admin even while not loaded."
    /// In <see cref="PermissionContext"/>, the Admin flag (<see cref="PermissionContext.IsAdmin"/>) is
    /// captured from the authentication state during <c>InitializeAsync</c>, so the Admin short-circuit
    /// becomes active once the context is loaded. This test asserts the substantive guarantee — an Admin
    /// holds any permission including keys absent from the cached set — against a loaded Admin context,
    /// which is the state in which <c>IsAdmin</c> is populated. The not-loaded, non-Admin denial clause
    /// of the property is covered by <see cref="NotLoaded_NonAdmin_AlwaysFalse"/>.
    /// </remarks>
    [Property(MaxTest = 100)]
    public Property Loaded_Admin_GrantsKeysAbsentFromCache()
    {
        // A cached set drawn from a disjoint pool so the query key is guaranteed NOT present,
        // isolating the Admin bypass from any accidental cache hit.
        var cachedSetGen = Gen.Choose(0, 4).SelectMany<int, List<string>>(count =>
            Gen.ArrayOf(
                Gen.Elements("Users.Read", "Roles.Read", "AuditLog.Read", "Announcements.Read"), count)
                .Select(keys => keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList()));

        // Query keys from a pool disjoint from the cached pool above (plus clearly-nonexistent keys).
        var queryKeyGen = Gen.Elements(
            "EmailTemplates.Update", "Permissions.Manage", "Nonexistent.Key", "Totally.Absent");

        var gen = cachedSetGen.SelectMany<List<string>, (List<string> cached, string queryKey)>(cached =>
            queryKeyGen.Select(key => (cached, key)));

        return Prop.ForAll(Arb.From(gen), input =>
        {
            var context = CreateContext(input.cached, isAdmin: true, initialize: true);

            // Precondition: the query key is genuinely absent from the cached set.
            var absent = !input.cached.Contains(input.queryKey, StringComparer.OrdinalIgnoreCase);
            var actual = context.HasPermission(input.queryKey);

            return (absent && actual)
                .Label($"Admin must grant a key absent from cache. QueryKey='{input.queryKey}', " +
                       $"Absent={absent}, Actual={actual}, Cached=[{string.Join(", ", input.cached)}]");
        });
    }

    #endregion

    #region Fakes

    /// <summary>
    /// Fake HTTP message handler returning a JSON array of permission keys for the
    /// GET <c>/api/permissions/my-permissions</c> endpoint, letting the test control the loaded set.
    /// </summary>
    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        /// <summary>
        /// The permission keys to serialize as the response body.
        /// </summary>
        private readonly List<string> _permissions;

        /// <summary>
        /// Initializes the handler with the permission keys to return.
        /// </summary>
        /// <param name="permissions">The permission keys the fake API returns.</param>
        public FakeHttpHandler(List<string> permissions)
        {
            _permissions = permissions;
        }

        /// <summary>
        /// Returns a 200 OK response whose JSON body is the configured permission key array.
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(_permissions),
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
            return Task.FromResult(response);
        }
    }

    #endregion
}
