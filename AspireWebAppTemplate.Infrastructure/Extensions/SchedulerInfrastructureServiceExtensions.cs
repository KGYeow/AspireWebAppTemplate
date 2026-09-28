using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Infrastructure.Services.AuditLog;
using Microsoft.Extensions.DependencyInjection;

namespace AspireWebAppTemplate.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering the focused set of infrastructure services consumed by the
/// short-lived <c>AspireWebAppTemplate.Scheduler</c> batch host.
/// </summary>
/// <remarks>
/// This seam exists so Infrastructure continues to own its feature-service DI composition while giving
/// the Scheduler a registration surface that matches exactly what its registered job(s) consume — the
/// <see cref="IAuditLogRetentionService"/> only. It deliberately does NOT register the full API/Web
/// feature graph (users, roles, email, notifications, AI/Bedrock, LDAP, announcements, page permissions,
/// navigation, the HTML sanitizer, the Web callback client, ASP.NET Core Identity, Data Protection, or
/// <c>ICurrentUserAccessor</c>). Reintroduce a system <c>ICurrentUserAccessor</c> and the audit-write
/// dependencies only when a future job performs an auditable write. This keeps
/// <see cref="InfrastructureServiceExtensions.AddInfrastructureServices"/> untouched and avoids forking it.
///
/// Like <see cref="InfrastructureServiceExtensions.AddInfrastructureServices"/>, this is a pure
/// service-registration extension: the composition root (the Scheduler host) owns reading the
/// connection string and registering the <c>ApplicationDbContext</c>, mirroring how the API's
/// <c>Program.cs</c> wires the database.
/// </remarks>
public static class SchedulerInfrastructureServiceExtensions
{
    /// <summary>
    /// Registers the minimal feature services the Scheduler's batch jobs consume: the
    /// <see cref="IAuditLogRetentionService"/> used by the audit-log purge job. The
    /// <c>ApplicationDbContext</c> it depends on is registered by the Scheduler host (composition root),
    /// consistent with the API.
    /// </summary>
    /// <param name="services">The service collection to add the registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so calls can be chained.</returns>
    public static IServiceCollection AddSchedulerInfrastructure(this IServiceCollection services)
    {
        // Audit-log retention: the only feature service the Scheduler's purge job consumes. Depends
        // solely on ApplicationDbContext + IConfiguration, so no Identity/UserManager is required.
        services.AddScoped<IAuditLogRetentionService, AuditLogRetentionService>();

        return services;
    }
}