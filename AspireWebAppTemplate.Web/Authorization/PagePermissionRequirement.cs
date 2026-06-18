using Microsoft.AspNetCore.Authorization;

namespace AspireWebAppTemplate.Web.Authorization;

/// <summary>
/// Authorization requirement that triggers the <see cref="PagePermissionHandler"/> to evaluate
/// whether the current user has permission to access the requested page route.
/// </summary>
/// <remarks>
/// <para>
/// This requirement is added to the authorization policy applied to Blazor page routes.
/// It carries no configuration data — all evaluation logic resides in the handler.
/// </para>
/// <para>
/// The handler evaluates access using a four-step algorithm:
/// Admin role → System_Page → cached permission check → path undetermined (allow).
/// </para>
/// </remarks>
public class PagePermissionRequirement : IAuthorizationRequirement
{
}
