// Feature: page-access-permissions, Property 5: Case-Insensitive Permission Lookup
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Web.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using Gen = FsCheck.Fluent.Gen;
using Property = FsCheck.Property;

namespace AspireWebAppTemplate.Tests.PagePermissions.Properties;

/// <summary>
/// Property-based tests verifying that PagePermissionContext.CanAccess performs
/// case-insensitive lookups using OrdinalIgnoreCase comparison, and correctly
/// denies access to paths not in the cache.
/// </summary>
/// <remarks>
/// **Validates: Requirements 5.3, 6.6, 12.1**
/// </remarks>
public class PagePermissionContextPropertyTests
{
    /// <summary>
    /// System pages defined in PagePermissionContext — these are excluded from the test
    /// because they always return true regardless of cache state.
    /// </summary>
    private static readonly HashSet<string> SystemPages = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Account/Login",
        "/Account/Register",
        "/Account/AccessDenied",
        "/Error",
        "/Account/ForgotPassword",
        "/Account/ResetPassword",
        "/Account/PerformLogin"
    };

    /// <summary>
    /// Creates a PagePermissionContext initialized with the given set of accessible page paths.
    /// Mocks the ApiPagePermissionService to return the provided paths and simulates an
    /// authenticated user so that InitializeAsync populates the cache.
    /// </summary>
    private static PagePermissionContext CreateInitializedContext(List<string> accessiblePages)
    {
        // Mock ApiPagePermissionService to return the specified pages
        var httpClient = new HttpClient(new FakeHttpHandler(accessiblePages))
        {
            BaseAddress = new Uri("https://localhost")
        };
        var apiService = new ApiPagePermissionService(httpClient);

        // Mock AuthenticationStateProvider to return an authenticated user
        var authStateProviderMock = new Mock<AuthenticationStateProvider>();
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, "testuser"));
        var principal = new ClaimsPrincipal(identity);
        var authState = new AuthenticationState(principal);
        authStateProviderMock
            .Setup(a => a.GetAuthenticationStateAsync())
            .ReturnsAsync(authState);

        var context = new PagePermissionContext(
            apiService,
            authStateProviderMock.Object,
            NullLogger<PagePermissionContext>.Instance);

        // Initialize the context to populate the cache
        context.InitializeAsync().GetAwaiter().GetResult();

        return context;
    }

    /// <summary>
    /// Applies a random case mutation to each character in the input string.
    /// Each character is independently uppercased or lowercased based on the provided
    /// boolean array (true = uppercase, false = lowercase).
    /// </summary>
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
    /// Property: For any page path stored in the permission cache and any case variation
    /// of that path, CanAccess SHALL return true. Conversely, for any path NOT in the cache
    /// (and not a System_Page), CanAccess SHALL return false regardless of casing.
    /// **Validates: Requirements 5.3, 6.6, 12.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public Property CanAccess_IsCaseInsensitive_ForCachedPaths()
    {
        // Generator for valid page path segments (alphanumeric, 1-10 chars)
        var segmentGen = Gen.Elements("admin", "dashboard", "settings", "users", "reports",
            "counter", "weather", "audit", "roles", "profile", "pages", "config");

        // Generator for page paths: "/" followed by 1-3 segments joined by "/"
        var pagePathGen = Gen.Choose(1, 3).SelectMany<int, string>(segmentCount =>
            Gen.ArrayOf<string>(segmentGen, segmentCount)
                .Select(segments => "/" + string.Join("/", segments)));

        // Generator for a list of 1-5 unique page paths (the accessible pages set)
        var accessiblePagesGen = Gen.Choose(1, 5).SelectMany<int, List<string>>(count =>
            Gen.ArrayOf<string>(pagePathGen, count + 2) // generate extras to ensure uniqueness after dedup
                .Select(paths => paths
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(p => !SystemPages.Contains(p)) // exclude system pages from test set
                    .Take(count)
                    .ToList()));

        // Generator for case mutation booleans (one per character, up to 30 chars)
        var mutationGen = Gen.ArrayOf<bool>(Gen.Elements(true, false), 30);

        // Generator for a path NOT in the accessible set (different segments)
        var nonAccessibleSegmentGen = Gen.Elements("unknown", "forbidden", "blocked",
            "restricted", "hidden", "secret", "private", "internal");
        var nonAccessiblePathGen = Gen.Choose(1, 3).SelectMany<int, string>(segmentCount =>
            Gen.ArrayOf<string>(nonAccessibleSegmentGen, segmentCount)
                .Select(segments => "/" + string.Join("/", segments)));

        var gen = accessiblePagesGen.SelectMany<List<string>, (List<string> pages, bool[] mutations, string nonAccessiblePath)>(pages =>
            mutationGen.SelectMany<bool[], (List<string> pages, bool[] mutations, string nonAccessiblePath)>(mutations =>
                nonAccessiblePathGen.Select(nonAccessiblePath =>
                    (pages, mutations, nonAccessiblePath))));

        return Prop.ForAll(Arb.From(gen),
            ((List<string> pages, bool[] mutations, string nonAccessiblePath) input) =>
        {
            // Skip if no accessible pages were generated (after dedup/filtering)
            if (input.pages.Count == 0)
                return true.Label("Skipped: no accessible pages generated");

            var context = CreateInitializedContext(input.pages);

            // Test 1: Each accessible path with random case mutation should return true
            var allAccessiblePass = true;
            var failedPath = "";
            foreach (var path in input.pages)
            {
                var mutated = ApplyCaseMutation(path, input.mutations);
                if (!context.CanAccess(mutated))
                {
                    allAccessiblePass = false;
                    failedPath = $"Original='{path}', Mutated='{mutated}'";
                    break;
                }
            }

            // Test 2: Non-accessible path should return false (regardless of case)
            // Only test if it's not accidentally a system page or in the accessible set
            var nonAccessibleResult = true;
            if (!SystemPages.Contains(input.nonAccessiblePath) &&
                !input.pages.Contains(input.nonAccessiblePath, StringComparer.OrdinalIgnoreCase))
            {
                var mutatedNonAccessible = ApplyCaseMutation(input.nonAccessiblePath, input.mutations);
                if (context.CanAccess(mutatedNonAccessible))
                {
                    nonAccessibleResult = false;
                }
            }

            return (allAccessiblePass && nonAccessibleResult)
                .Label($"AccessiblePass={allAccessiblePass} (failed: {failedPath}), " +
                       $"NonAccessibleDenied={nonAccessibleResult}, " +
                       $"Pages=[{string.Join(", ", input.pages)}], " +
                       $"NonAccessible='{input.nonAccessiblePath}'");
        });
    }

    // Feature: page-access-permissions, Property 4: System Pages Always Accessible
    /// <summary>
    /// Property: For any System_Page path and empty cache state (not initialized),
    /// CanAccess SHALL return true.
    /// **Validates: Requirements 5.6, 6.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public Property SystemPages_AlwaysAccessible_WhenCacheNotInitialized()
    {
        // Generate a random system page from the list
        var systemPageGen = Gen.Elements(SystemPagesList);

        return Prop.ForAll(Arb.From(systemPageGen), (string systemPage) =>
        {
            // Create context but do NOT call InitializeAsync — cache is empty/uninitialized
            var httpClient = new HttpClient(new FakeHttpHandler([]))
            {
                BaseAddress = new Uri("https://localhost")
            };
            var apiService = new ApiPagePermissionService(httpClient);

            var authStateProviderMock = new Mock<AuthenticationStateProvider>();
            var identity = new ClaimsIdentity("TestAuth");
            identity.AddClaim(new Claim(ClaimTypes.Name, "testuser"));
            var principal = new ClaimsPrincipal(identity);
            var authState = new AuthenticationState(principal);
            authStateProviderMock
                .Setup(a => a.GetAuthenticationStateAsync())
                .ReturnsAsync(authState);

            var context = new PagePermissionContext(
                apiService,
                authStateProviderMock.Object,
                NullLogger<PagePermissionContext>.Instance);

            // IsLoaded should be false (not initialized)
            var notLoaded = !context.IsLoaded;

            // System_Pages should still be accessible even without initialization
            var canAccess = context.CanAccess(systemPage);

            return (notLoaded && canAccess)
                .Label($"SystemPage='{systemPage}', IsLoaded={context.IsLoaded}, CanAccess={canAccess}");
        });
    }

    // Feature: page-access-permissions, Property 4: System Pages Always Accessible
    /// <summary>
    /// Property: For any System_Page path and partial cache state (some random non-system
    /// paths loaded), CanAccess SHALL return true.
    /// **Validates: Requirements 5.6, 6.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public Property SystemPages_AlwaysAccessible_WhenCachePartiallyLoaded()
    {
        // Non-system pages used to populate a partial cache
        var nonSystemPages = new[]
        {
            "/counter", "/weather", "/dashboard", "/admin/audit-log",
            "/admin/user-management", "/admin/role-management",
            "/admin/page-permissions", "/reports", "/account/settings", "/account/profile"
        };

        // Generate a random system page and a random non-empty subset of non-system pages
        var systemPageGen = Gen.Elements(SystemPagesList);
        var partialCacheGen = Gen.SubListOf(nonSystemPages)
            .Where(list => list.Count > 0 && list.Count < nonSystemPages.Length)
            .Select(list => list.ToList());

        var combinedGen = systemPageGen.SelectMany<string, (string systemPage, List<string> cachedPages)>(sp =>
            partialCacheGen.Select(cache => (sp, cache)));

        return Prop.ForAll(Arb.From(combinedGen),
            ((string systemPage, List<string> cachedPages) input) =>
        {
            // Create context with partial cache (some non-system pages loaded)
            var context = CreateInitializedContext(input.cachedPages);

            // System_Pages should be accessible regardless of what's in the cache
            var canAccess = context.CanAccess(input.systemPage);
            var isLoaded = context.IsLoaded;

            return (isLoaded && canAccess)
                .Label($"SystemPage='{input.systemPage}', CachedPages={input.cachedPages.Count}, " +
                       $"IsLoaded={isLoaded}, CanAccess={canAccess}");
        });
    }

    // Feature: page-access-permissions, Property 4: System Pages Always Accessible
    /// <summary>
    /// Property: For any System_Page path and full cache state (all non-system pages loaded),
    /// CanAccess SHALL return true.
    /// **Validates: Requirements 5.6, 6.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public Property SystemPages_AlwaysAccessible_WhenCacheFullyLoaded()
    {
        // All non-system pages to simulate a full cache
        var allNonSystemPages = new List<string>
        {
            "/counter", "/weather", "/dashboard", "/admin/audit-log",
            "/admin/user-management", "/admin/role-management",
            "/admin/page-permissions", "/reports", "/account/settings", "/account/profile"
        };

        // Generate a random system page; full cache means all non-system pages are loaded
        var systemPageGen = Gen.Elements(SystemPagesList);

        return Prop.ForAll(Arb.From(systemPageGen), (string systemPage) =>
        {
            // Create context with full cache (all non-system pages loaded)
            var context = CreateInitializedContext(allNonSystemPages);

            // System_Pages should be accessible even with full cache
            var canAccess = context.CanAccess(systemPage);
            var isLoaded = context.IsLoaded;

            return (isLoaded && canAccess)
                .Label($"SystemPage='{systemPage}', IsLoaded={isLoaded}, CanAccess={canAccess}");
        });
    }

    // Feature: page-access-permissions, Property 4: System Pages Always Accessible
    /// <summary>
    /// Property: Case variations of System_Page paths should also be accessible due to
    /// OrdinalIgnoreCase comparison in the SystemPages HashSet.
    /// **Validates: Requirements 5.6, 6.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public Property SystemPages_AlwaysAccessible_WithCaseVariations()
    {
        // Generator for case mutation booleans (one per character, up to 30 chars)
        var mutationGen = Gen.ArrayOf<bool>(Gen.Elements(true, false), 30);

        // Generate a random system page and a case mutation array
        var gen = Gen.Elements(SystemPagesList)
            .SelectMany<string, (string original, bool[] mutations)>(sp =>
                mutationGen.Select(m => (sp, m)));

        return Prop.ForAll(Arb.From(gen),
            ((string original, bool[] mutations) input) =>
        {
            // Create context with partial cache to show it's independent of cache content
            var context = CreateInitializedContext(["/counter", "/weather"]);

            // Apply case mutation to the system page path
            var variant = ApplyCaseMutation(input.original, input.mutations);

            // Case variant of a System_Page should also be accessible
            var canAccess = context.CanAccess(variant);

            return canAccess
                .Label($"Original='{input.original}', Variant='{variant}', CanAccess={canAccess}");
        });
    }

    /// <summary>
    /// The list of System_Page paths as an array for use with Gen.Elements.
    /// </summary>
    private static readonly string[] SystemPagesList =
    [
        "/Account/Login",
        "/Account/Register",
        "/Account/AccessDenied",
        "/Error",
        "/Account/ForgotPassword",
        "/Account/ResetPassword",
        "/Account/PerformLogin"
    ];

    /// <summary>
    /// Fake HTTP message handler that returns a JSON list of page paths
    /// when the /api/page-permissions/my-pages endpoint is called.
    /// </summary>
    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly List<string> _pages;

        public FakeHttpHandler(List<string> pages)
        {
            _pages = pages;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(_pages),
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
