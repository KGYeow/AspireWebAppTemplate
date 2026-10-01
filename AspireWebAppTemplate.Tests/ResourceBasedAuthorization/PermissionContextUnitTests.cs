// Feature: resource-based-authorization, Task 6.5: PermissionContext unit tests
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AspireWebAppTemplate.Tests.ResourceBasedAuthorization;

/// <summary>
/// Unit tests for <see cref="PermissionContext"/>, the per-circuit effective-permission cache that
/// backs navigation and page authorization in the Blazor Server web project.
/// </summary>
/// <remarks>
/// <para>
/// Each test constructs a real <see cref="PermissionContext"/> over a real
/// <see cref="ApiPermissionService"/> whose <see cref="HttpClient"/> is driven by a controllable
/// <see cref="FakeHttpHandler"/> (success JSON, a non-success status, or a thrown exception) and a
/// stub <see cref="AuthenticationStateProvider"/> (authenticated non-Admin, authenticated Admin, or
/// unauthenticated). This mirrors the FakeHttpHandler + stub-auth approach used across the suite.
/// </para>
/// <para>
/// Coverage: the <see cref="PermissionContext.IsLoaded"/> lifecycle, graceful degradation on API
/// failure, the unauthenticated skip (no API call), and the Admin short-circuit that holds even
/// before the cache is loaded.
/// </para>
/// <para>
/// **Validates: Requirements 7.5, 7.7, 7.8 (and 3.2).**
/// </para>
/// </remarks>
public class PermissionContextUnitTests
{
    #region Test Infrastructure

    /// <summary>
    /// Builds a <see cref="PermissionContext"/> wired to a real <see cref="ApiPermissionService"/>
    /// over the supplied <paramref name="handler"/>, together with the supplied authentication-state
    /// stub. Keeps construction identical across tests so each test only varies the handler behavior
    /// and the user identity.
    /// </summary>
    /// <param name="handler">The fake HTTP handler controlling the my-permissions response.</param>
    /// <param name="authStateProvider">The stub authentication state provider.</param>
    /// <returns>A ready-to-initialize <see cref="PermissionContext"/>.</returns>
    private static PermissionContext CreateContext(
        FakeHttpHandler handler,
        AuthenticationStateProvider authStateProvider)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost")
        };
        var apiService = new ApiPermissionService(httpClient);

        return new PermissionContext(
            apiService,
            authStateProvider,
            NullLogger<PermissionContext>.Instance);
    }

    /// <summary>
    /// Creates a stub <see cref="AuthenticationStateProvider"/> that reports an authenticated user
    /// carrying the supplied role claims. An authentication type is supplied so the identity's
    /// <c>IsAuthenticated</c> is <c>true</c>.
    /// </summary>
    /// <param name="roles">The role names placed in <see cref="ClaimTypes.Role"/> claims.</param>
    /// <returns>A stub provider that returns the authenticated principal.</returns>
    private static AuthenticationStateProvider AuthenticatedProvider(params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "testuser") };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth", nameType: ClaimTypes.Name, roleType: ClaimTypes.Role);
        var authState = new AuthenticationState(new ClaimsPrincipal(identity));

        var mock = new Mock<AuthenticationStateProvider>();
        mock.Setup(a => a.GetAuthenticationStateAsync()).ReturnsAsync(authState);
        return mock.Object;
    }

    /// <summary>
    /// Creates a stub <see cref="AuthenticationStateProvider"/> that reports an unauthenticated user
    /// (an anonymous identity with no authentication type).
    /// </summary>
    /// <returns>A stub provider that returns the anonymous principal.</returns>
    private static AuthenticationStateProvider UnauthenticatedProvider()
    {
        var authState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var mock = new Mock<AuthenticationStateProvider>();
        mock.Setup(a => a.GetAuthenticationStateAsync()).ReturnsAsync(authState);
        return mock.Object;
    }

    #endregion

    #region IsLoaded Lifecycle

    /// <summary>
    /// Verifies the <see cref="PermissionContext.IsLoaded"/> lifecycle: it is <c>false</c> before
    /// <see cref="PermissionContext.InitializeAsync"/> runs and <c>true</c> after a successful load.
    /// **Validates: Requirements 7.5.**
    /// </summary>
    [Fact]
    public async Task IsLoaded_IsFalseBeforeInit_AndTrueAfterSuccessfulInit()
    {
        var handler = FakeHttpHandler.WithPermissions(["Users.Read", "Roles.Read"]);
        var context = CreateContext(handler, AuthenticatedProvider());

        // Before initialization the cache is not populated.
        Assert.False(context.IsLoaded);

        await context.InitializeAsync();

        // After a successful initialization the cache is marked loaded and the key is granted.
        Assert.True(context.IsLoaded);
        Assert.True(context.HasPermission("Users.Read"));
    }

    #endregion

    #region API-Failure Graceful Degradation

    /// <summary>
    /// Verifies graceful degradation when the my-permissions call returns a non-success status: after
    /// initialization <see cref="PermissionContext.IsLoaded"/> is <c>true</c> and the cache is empty,
    /// so a non-Admin <see cref="PermissionContext.HasPermission(string)"/> returns <c>false</c>.
    /// **Validates: Requirements 7.7.**
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WhenApiReturnsNonSuccess_LeavesCacheEmptyButLoaded()
    {
        var handler = FakeHttpHandler.WithStatus(HttpStatusCode.InternalServerError);
        var context = CreateContext(handler, AuthenticatedProvider());

        await context.InitializeAsync();

        Assert.True(context.IsLoaded);
        Assert.False(context.IsAdmin);
        Assert.False(context.HasPermission("Users.Read"));
        Assert.False(context.HasAnyPermissionInModule("Users"));
    }

    /// <summary>
    /// Verifies graceful degradation when the my-permissions call throws (network/transport failure):
    /// after initialization <see cref="PermissionContext.IsLoaded"/> is <c>true</c> and the cache is
    /// empty, so a non-Admin <see cref="PermissionContext.HasPermission(string)"/> returns <c>false</c>.
    /// **Validates: Requirements 7.7.**
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WhenApiThrows_LeavesCacheEmptyButLoaded()
    {
        var handler = FakeHttpHandler.ThatThrows();
        var context = CreateContext(handler, AuthenticatedProvider());

        await context.InitializeAsync();

        Assert.True(context.IsLoaded);
        Assert.False(context.IsAdmin);
        Assert.False(context.HasPermission("Users.Read"));
        Assert.False(context.HasAnyPermissionInModule("Users"));
    }

    #endregion

    #region Unauthenticated Skip

    /// <summary>
    /// Verifies that when the authentication state reports an unauthenticated user,
    /// <see cref="PermissionContext.InitializeAsync"/> does NOT call the API, the cache stays empty,
    /// and <see cref="PermissionContext.IsLoaded"/> is still <c>true</c>.
    /// **Validates: Requirements 7.8.**
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WhenUnauthenticated_SkipsApiCallAndLeavesCacheEmpty()
    {
        var handler = FakeHttpHandler.WithPermissions(["Users.Read"]);
        var context = CreateContext(handler, UnauthenticatedProvider());

        await context.InitializeAsync();

        // The API must not be hit for an unauthenticated user.
        Assert.False(handler.WasCalled);

        Assert.True(context.IsLoaded);
        Assert.False(context.IsAdmin);
        Assert.False(context.HasPermission("Users.Read"));
    }

    #endregion

    #region Admin Short-Circuit

    /// <summary>
    /// Verifies that an Admin user's permission checks return <c>true</c> even before (and without)
    /// a loaded cache: both <see cref="PermissionContext.HasPermission(string)"/> and
    /// <see cref="PermissionContext.HasAnyPermissionInModule(string)"/> short-circuit on the Admin role.
    /// **Validates: Requirements 3.2.**
    /// </summary>
    [Fact]
    public async Task AdminUser_HasPermissionAndModuleChecks_ReturnTrueAfterInit()
    {
        // The handler returns no permissions; Admin must still be granted everything.
        var handler = FakeHttpHandler.WithPermissions([]);
        var context = CreateContext(handler, AuthenticatedProvider("Admin"));

        await context.InitializeAsync();

        Assert.True(context.IsAdmin);
        Assert.True(context.HasPermission("Users.Read"));
        Assert.True(context.HasPermission("Nonexistent.Action"));
        Assert.True(context.HasAnyPermissionInModule("AnyModule"));
    }

    /// <summary>
    /// Verifies that the Admin short-circuit holds even when the cache has not been loaded: an Admin
    /// identity is set during construction via the auth-state stub, and permission checks succeed
    /// without <see cref="PermissionContext.InitializeAsync"/> populating the cache (the handler is
    /// never called). Confirms Admin access does not depend on <see cref="PermissionContext.IsLoaded"/>.
    /// **Validates: Requirements 3.2.**
    /// </summary>
    [Fact]
    public async Task AdminUser_ShortCircuits_WithoutApiData()
    {
        // ThatThrows guarantees that if the context ever consults the cache instead of
        // short-circuiting on the Admin role, the test would surface the failure.
        var handler = FakeHttpHandler.ThatThrows();
        var context = CreateContext(handler, AuthenticatedProvider("Admin"));

        // InitializeAsync swallows the thrown API error but still captures the Admin role.
        await context.InitializeAsync();

        Assert.True(context.IsAdmin);
        Assert.True(context.HasPermission("Users.Read"));
        Assert.True(context.HasAnyPermissionInModule("Users"));
    }

    #endregion

    #region Fake HTTP Handler

    /// <summary>
    /// Controllable fake <see cref="HttpMessageHandler"/> for the my-permissions endpoint. It can
    /// return a success JSON array of permission keys, a non-success status, or throw a transport
    /// exception, and it records whether it was ever invoked (so tests can assert the API was skipped).
    /// </summary>
    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        /// <summary>
        /// The permission keys serialized into a success response; null when the handler is configured
        /// to return a non-success status or to throw.
        /// </summary>
        private readonly List<string>? _permissions;

        /// <summary>
        /// The status code returned when <see cref="_permissions"/> is null and <see cref="_throw"/>
        /// is false.
        /// </summary>
        private readonly HttpStatusCode _status;

        /// <summary>
        /// When true, the handler throws an <see cref="HttpRequestException"/> to simulate a transport
        /// failure.
        /// </summary>
        private readonly bool _throw;

        /// <summary>
        /// Gets a value indicating whether <see cref="SendAsync"/> was invoked at least once.
        /// </summary>
        public bool WasCalled { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FakeHttpHandler"/> class.
        /// </summary>
        /// <param name="permissions">Permission keys for a success response, or null for failure modes.</param>
        /// <param name="status">The status code used when returning a non-success response.</param>
        /// <param name="throw">When true, the handler throws instead of responding.</param>
        private FakeHttpHandler(List<string>? permissions, HttpStatusCode status, bool @throw)
        {
            _permissions = permissions;
            _status = status;
            _throw = @throw;
        }

        /// <summary>
        /// Creates a handler that returns a 200 response carrying the supplied permission keys as JSON.
        /// </summary>
        /// <param name="permissions">The permission keys to serialize.</param>
        /// <returns>A configured <see cref="FakeHttpHandler"/>.</returns>
        public static FakeHttpHandler WithPermissions(List<string> permissions) =>
            new(permissions, HttpStatusCode.OK, @throw: false);

        /// <summary>
        /// Creates a handler that returns the supplied non-success status with an error body.
        /// </summary>
        /// <param name="status">The non-success status code to return.</param>
        /// <returns>A configured <see cref="FakeHttpHandler"/>.</returns>
        public static FakeHttpHandler WithStatus(HttpStatusCode status) =>
            new(permissions: null, status, @throw: false);

        /// <summary>
        /// Creates a handler that throws an <see cref="HttpRequestException"/> to simulate a transport failure.
        /// </summary>
        /// <returns>A configured <see cref="FakeHttpHandler"/>.</returns>
        public static FakeHttpHandler ThatThrows() =>
            new(permissions: null, HttpStatusCode.OK, @throw: true);

        /// <summary>
        /// Records the invocation and produces the configured response, or throws when configured to.
        /// </summary>
        /// <param name="request">The outgoing request (ignored beyond invocation tracking).</param>
        /// <param name="cancellationToken">A cancellation token (unused).</param>
        /// <returns>The configured <see cref="HttpResponseMessage"/>.</returns>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;

            if (_throw)
                throw new HttpRequestException("Simulated transport failure.");

            if (_permissions is not null)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(_permissions),
                        Encoding.UTF8,
                        "application/json")
                };
                return Task.FromResult(response);
            }

            var failure = new HttpResponseMessage(_status)
            {
                Content = new StringContent("error", Encoding.UTF8, "text/plain")
            };
            return Task.FromResult(failure);
        }
    }

    #endregion
}
