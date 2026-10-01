using Microsoft.AspNetCore.Authorization;

namespace AspireWebAppTemplate.Web.Authorization;

/// <summary>
/// Authorization requirement that triggers the <see cref="PageAccessAuthorizationHandler"/> to evaluate
/// whether the current user has permission to access the requested page route.
/// </summary>
/// <remarks>
/// <para>
/// This requirement is added to the authorization policy applied to Blazor page routes.
/// It carries no configuration data — all evaluation logic resides in the handler.
/// </para>
/// <para>
/// The handler evaluates access using a five-step, module-based flow:
/// Admin role → path undetermined (allow) → System_Page → cache not loaded (allow) → module permission check.
/// </para>
/// </remarks>
public class PageAccessRequirement : IAuthorizationRequirement
{
}
