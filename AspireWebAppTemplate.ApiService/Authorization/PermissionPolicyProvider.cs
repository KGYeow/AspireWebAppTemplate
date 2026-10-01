using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AspireWebAppTemplate.ApiService.Authorization;

/// <summary>
/// Dynamic authorization policy provider that materializes a policy on demand for any
/// permission-key policy name (<c>Module.Action</c>, e.g. <c>Users.Read</c>). This removes the
/// need to register every permission as a named policy at startup: a controller can simply declare
/// <c>[Authorize(Policy = "Users.Read")]</c> and this provider builds a policy carrying a
/// <see cref="PermissionRequirement"/> for that key.
///
/// For policy names that are not permission keys — plain <c>[Authorize]</c> (the default policy),
/// fallback policies, and explicitly registered named policies such as <c>InternalApiPolicy</c> —
/// the provider defers to the wrapped <see cref="DefaultAuthorizationPolicyProvider"/>, so existing
/// authorization continues to work unchanged.
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    #region Constructor

    /// <summary>
    /// Matches a permission-key policy name: exactly one dot separating two PascalCase segments,
    /// each starting with an uppercase letter and 1–50 characters long. Mirrors the permission-key
    /// format so only genuine permission keys are handled dynamically; everything else falls back
    /// to the default provider.
    /// </summary>
    private static readonly Regex PermissionKeyPattern =
        new("^[A-Z][a-zA-Z0-9]{0,49}\\.[A-Z][a-zA-Z0-9]{0,49}$", RegexOptions.Compiled);

    /// <summary>
    /// The wrapped default provider used for the default policy, the fallback policy, and any
    /// policy name this provider does not own (e.g. explicitly registered named policies).
    /// </summary>
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionPolicyProvider"/> class.
    /// </summary>
    /// <param name="options">The authorization options used to construct the wrapped default provider.</param>
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    #endregion

    #region Policy Resolution

    /// <summary>
    /// Returns a policy for the given name. When the name is a permission key (<c>Module.Action</c>),
    /// a policy carrying a <see cref="PermissionRequirement"/> for that key is built dynamically.
    /// Otherwise resolution is delegated to the wrapped default provider so registered named policies
    /// (e.g. <c>InternalApiPolicy</c>) still resolve.
    /// </summary>
    /// <param name="policyName">The policy name declared on the endpoint.</param>
    /// <returns>The resolved <see cref="AuthorizationPolicy"/>, or <c>null</c> if no policy matches.</returns>
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!string.IsNullOrWhiteSpace(policyName) && PermissionKeyPattern.IsMatch(policyName))
        {
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallbackProvider.GetPolicyAsync(policyName);
    }

    /// <summary>
    /// Returns the default policy (applied by a bare <c>[Authorize]</c>) by delegating to the
    /// wrapped default provider.
    /// </summary>
    /// <returns>The default <see cref="AuthorizationPolicy"/>.</returns>
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallbackProvider.GetDefaultPolicyAsync();

    /// <summary>
    /// Returns the fallback policy (applied when no other policy is specified) by delegating to the
    /// wrapped default provider.
    /// </summary>
    /// <returns>The fallback <see cref="AuthorizationPolicy"/>, or <c>null</c> if none is configured.</returns>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallbackProvider.GetFallbackPolicyAsync();

    #endregion
}
