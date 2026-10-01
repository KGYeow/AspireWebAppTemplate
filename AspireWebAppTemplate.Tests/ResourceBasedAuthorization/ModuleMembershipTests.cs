// Feature: resource-based-authorization, Property 4: Module membership determines page visibility
using System.Security.Claims;
using AspireWebAppTemplate.Web.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Gen = FsCheck.Fluent.Gen;

namespace AspireWebAppTemplate.Tests.ResourceBasedAuthorization;

/// <summary>
/// Property-based tests verifying that <see cref="PermissionContext.HasAnyPermissionInModule(string)"/>
/// reports module membership correctly: for any cached permission set and any module name, the method
/// returns <c>true</c> if and only if the set contains at least one key beginning with
/// <c>module + "."</c> (case-insensitive), OR the current user holds the Admin role.
/// </summary>
/// <remarks>
/// <para>**Validates: Requirements 6.2, 6.6, 7.3**</para>
/// <para>
/// Property 4: module membership determines page visibility. The trailing dot in the <c>module + "."</c>
/// prefix is a deliberate guard: a module named <c>"User"</c> must NOT match a key such as
/// <c>"Users.Read"</c>. The tests generate random permission sets and module names (both present and
/// absent, including a prefix-without-dot module) and assert the result equals an independent oracle:
/// <c>any key starts with module + "." (OrdinalIgnoreCase) OR IsAdmin</c>.
/// </para>
/// <para>
/// Infrastructure mirrors the sibling tasks: a real <see cref="ApiPermissionService"/> over a fake
/// <see cref="System.Net.Http.HttpMessageHandler"/> returning a controlled <c>string[]</c> for
/// GET /api/permissions/my-permissions, plus a stub <see cref="AuthenticationStateProvider"/> for
/// Admin / non-Admin principals. <see cref="PermissionContext.InitializeAsync"/> loads the cache
/// before each check.
/// </para>
/// </remarks>
public class ModuleMembershipTests
{
    #region Infrastructure Helpers

    /// <summary>
    /// Creates a <see cref="PermissionContext"/> whose per-circuit cache has been populated with the
    /// supplied permission keys, as an authenticated user who is Admin or non-Admin per
    /// <paramref name="isAdmin"/>. The permission set is delivered through a real
    /// <see cref="ApiPermissionService"/> backed by a fake HTTP handler, and
    /// <see cref="PermissionContext.InitializeAsync"/> is awaited so the cache is loaded.
    /// </summary>
    /// <param name="permissions">The effective permission keys the API should return.</param>
    /// <param name="isAdmin">Whether the stubbed principal holds the Admin role claim.</param>
    /// <returns>An initialized <see cref="PermissionContext"/> ready for membership checks.</returns>
    private static PermissionContext CreateInitializedContext(IEnumerable<string> permissions, bool isAdmin)
    {
        // Real ApiPermissionService over a fake handler returning the controlled permission list.
        var httpClient = new HttpClient(new FakeHttpHandler(permissions.ToList()))
        {
            BaseAddress = new Uri("https://localhost")
        };
        var apiService = new ApiPermissionService(httpClient);

        // Stub AuthenticationStateProvider for an authenticated Admin / non-Admin principal.
        var authStateProvider = CreateAuthStateProvider(isAdmin);

        var context = new PermissionContext(
            apiService,
            authStateProvider,
            NullLogger<PermissionContext>.Instance);

        // Load the cache (and capture Admin membership) before any membership check.
        context.InitializeAsync().GetAwaiter().GetResult();

        return context;
    }

    /// <summary>
    /// Builds a stub <see cref="AuthenticationStateProvider"/> that returns an authenticated principal.
    /// When <paramref name="isAdmin"/> is <c>true</c> the principal carries an "Admin" role claim so
    /// <see cref="PermissionContext"/> captures Admin membership during initialization.
    /// </summary>
    /// <param name="isAdmin">Whether to add the Admin role claim.</param>
    /// <returns>A mocked authentication state provider yielding the constructed principal.</returns>
    private static AuthenticationStateProvider CreateAuthStateProvider(bool isAdmin)
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, "testuser"));
        if (isAdmin)
            identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));

        var principal = new ClaimsPrincipal(identity);
        var authState = new AuthenticationState(principal);

        var mock = new Mock<AuthenticationStateProvider>();
        mock.Setup(a => a.GetAuthenticationStateAsync()).ReturnsAsync(authState);
        return mock.Object;
    }

    /// <summary>
    /// Independent oracle for module membership: returns <c>true</c> if the user is Admin, or if any
    /// permission key begins with <c>module + "."</c> under case-insensitive comparison. This mirrors
    /// the specification rather than the implementation so the test genuinely cross-checks behavior.
    /// </summary>
    /// <param name="permissions">The cached permission keys.</param>
    /// <param name="module">The module name under test.</param>
    /// <param name="isAdmin">Whether the user holds the Admin role.</param>
    /// <returns>The expected result of <see cref="PermissionContext.HasAnyPermissionInModule(string)"/>.</returns>
    private static bool ExpectedMembership(IEnumerable<string> permissions, string module, bool isAdmin)
    {
        if (isAdmin)
            return true;

        var prefix = module + ".";
        return permissions.Any(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Generators

    /// <summary>
    /// The fixed pool of module names used to compose permission keys and to probe membership.
    /// Includes "User" and "Users" so the prefix-without-dot guard can be exercised: a "User" probe
    /// must not match a "Users.*" key.
    /// </summary>
    private static readonly string[] Modules =
    [
        "Users", "User", "Roles", "AuditLog", "EmailTemplates", "Permissions", "Announcements"
    ];

    /// <summary>
    /// The fixed pool of action segments used to compose permission keys.
    /// </summary>
    private static readonly string[] Actions =
    [
        "Read", "Create", "Update", "Delete", "Manage", "Export", "Activate"
    ];

    /// <summary>
    /// Generates a single well-formed <c>Module.Action</c> permission key from the fixed pools.
    /// </summary>
    private static Gen<string> KeyGen()
    {
        return Gen.Elements(Modules).SelectMany(module =>
            Gen.Elements(Actions).Select(action => $"{module}.{action}"));
    }

    /// <summary>
    /// Generates a permission set of 0–8 keys (possibly empty, duplicates removed case-insensitively).
    /// </summary>
    private static Gen<List<string>> PermissionSetGen()
    {
        return Gen.Choose(0, 8).SelectMany(count =>
            Gen.ArrayOf(KeyGen(), count)
                .Select(keys => keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList()));
    }

    /// <summary>
    /// Generates a module name to probe. Draws from the known module pool (present cases, the
    /// prefix-without-dot "User" case) and from clearly absent names so both branches of the oracle
    /// are exercised.
    /// </summary>
    private static Gen<string> ProbeModuleGen()
    {
        var absent = Gen.Elements("Nonexistent", "Reports", "Billing", "Us", "UsersExtra", "Role");
        return Gen.OneOf(Gen.Elements(Modules), absent);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Property: for any non-Admin permission set and probe module,
    /// <see cref="PermissionContext.HasAnyPermissionInModule(string)"/> equals the oracle
    /// (any key starts with <c>module + "."</c>, case-insensitive). This covers membership for present
    /// modules, absence for unknown modules, and the trailing-dot guard (e.g. probing "User" against a
    /// "Users.*" key must return <c>false</c>).
    /// **Validates: Requirements 6.2, 6.6, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public FsCheck.Property NonAdmin_MembershipMatchesOracle()
    {
        var gen = PermissionSetGen().SelectMany(permissions =>
            ProbeModuleGen().Select(module => (permissions, module)));

        return Prop.ForAll(Arb.From(gen), input =>
        {
            var (permissions, module) = input;

            var context = CreateInitializedContext(permissions, isAdmin: false);

            var actual = context.HasAnyPermissionInModule(module);
            var expected = ExpectedMembership(permissions, module, isAdmin: false);

            return (actual == expected).Label(
                $"module='{module}', expected={expected}, actual={actual}, " +
                $"keys=[{string.Join(", ", permissions)}]");
        });
    }

    /// <summary>
    /// Property: an Admin user reports <c>true</c> for any module regardless of the cached permission
    /// set — including an empty set and modules that no key belongs to. Admins bypass module filtering.
    /// **Validates: Requirements 6.2, 6.6, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public FsCheck.Property Admin_ReturnsTrueForAnyModule()
    {
        var gen = PermissionSetGen().SelectMany(permissions =>
            ProbeModuleGen().Select(module => (permissions, module)));

        return Prop.ForAll(Arb.From(gen), input =>
        {
            var (permissions, module) = input;

            var context = CreateInitializedContext(permissions, isAdmin: true);

            var actual = context.HasAnyPermissionInModule(module);

            return actual.Label(
                $"Admin module='{module}', actual={actual}, keys=[{string.Join(", ", permissions)}]");
        });
    }

    /// <summary>
    /// Example: a non-Admin user with an empty permission set reports <c>false</c> for every module
    /// in the known pool. No module can be a member of an empty set.
    /// **Validates: Requirements 6.2, 6.6, 7.3**
    /// </summary>
    [Fact]
    public void NonAdmin_EmptySet_ReturnsFalseForEveryModule()
    {
        var context = CreateInitializedContext([], isAdmin: false);

        foreach (var module in Modules)
        {
            Assert.False(
                context.HasAnyPermissionInModule(module),
                $"Non-Admin empty set should deny module '{module}'.");
        }
    }

    /// <summary>
    /// Example: the trailing-dot guard. With a set containing only "Users.Read", probing the module
    /// "User" (a prefix of "Users" without the dot) must return <c>false</c>, while probing "Users"
    /// returns <c>true</c>.
    /// **Validates: Requirements 6.2, 6.6, 7.3**
    /// </summary>
    [Fact]
    public void NonAdmin_PrefixWithoutDot_DoesNotMatch()
    {
        var context = CreateInitializedContext(["Users.Read"], isAdmin: false);

        Assert.False(context.HasAnyPermissionInModule("User"),
            "'User' must not match 'Users.Read' because the trailing dot guards against prefix collisions.");
        Assert.True(context.HasAnyPermissionInModule("Users"),
            "'Users' must match 'Users.Read'.");
    }

    #endregion

    #region Fake HTTP Handler

    /// <summary>
    /// Fake HTTP message handler that returns the configured permission keys as a JSON string array
    /// for the GET /api/permissions/my-permissions call issued by <see cref="ApiPermissionService"/>.
    /// </summary>
    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        /// <summary>
        /// The permission keys serialized into every response body.
        /// </summary>
        private readonly List<string> _permissions;

        /// <summary>
        /// Initializes the handler with the permission keys to return.
        /// </summary>
        /// <param name="permissions">The permission keys the API should report.</param>
        public FakeHttpHandler(List<string> permissions)
        {
            _permissions = permissions;
        }

        /// <summary>
        /// Returns a 200 OK response whose JSON body is the configured permission key array,
        /// regardless of the request URI.
        /// </summary>
        /// <param name="request">The outgoing request (ignored).</param>
        /// <param name="cancellationToken">A cancellation token (unused).</param>
        /// <returns>A completed task yielding the canned response.</returns>
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
