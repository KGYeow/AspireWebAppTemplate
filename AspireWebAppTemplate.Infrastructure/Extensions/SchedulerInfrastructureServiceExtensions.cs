using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Services.AuditLog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AspireWebAppTemplate.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering the focused set of infrastructure services consumed by the
/// short-lived <c>AspireWebAppTemplate.Scheduler</c> batch host.
/// </summary>
/// <remarks>
/// This seam exists so Infrastructure continues to own its DI composition while giving the Scheduler
/// a registration surface that matches exactly what its registered job(s) consume — the
/// <see cref="ApplicationDbContext"/> and <see cref="IAuditLogRetentionService"/> only. It deliberately
/// does NOT register the full API/Web feature graph (users, roles, email, notifications, AI/Bedrock,
/// LDAP, announcements, page permissions, navigation, the HTML sanitizer, the Web callback client,
/// ASP.NET Core Identity, Data Protection, or <c>ICurrentUserAccessor</c>). Reintroduce a system
/// <c>ICurrentUserAccessor</c> and the audit-write dependencies only when a future job performs an
/// auditable write. This keeps <see cref="InfrastructureServiceExtensions.AddInfrastructureServices"/>
/// untouched and avoids forking it.
/// </remarks>
public static class SchedulerInfrastructureServiceExtensions
{
    /// <summary>
    /// Registers the minimal infrastructure services the Scheduler's batch jobs consume: the
    /// <see cref="ApplicationDbContext"/> (SQL Server, sharing the API's <c>DefaultConnection</c>) and
    /// the <see cref="IAuditLogRetentionService"/> used by the audit-log purge job.
    /// </summary>
    /// <param name="services">The service collection to add the registrations to.</param>
    /// <param name="configuration">The application configuration used to read the database connection string.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so calls can be chained.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the <c>DefaultConnection</c> connection string is not configured.
    /// </exception>
    public static IServiceCollection AddSchedulerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database — same connection string the API uses. The Scheduler never runs migrations; it
        // assumes the schema already exists (the API/deploy step owns migrations).
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, b => b.MigrationsAssembly("AspireWebAppTemplate.Infrastructure")));

        // Audit-log retention: the only feature service the Scheduler's purge job consumes. Depends
        // solely on ApplicationDbContext + IConfiguration, so no Identity/UserManager is required.
        services.AddScoped<IAuditLogRetentionService, AuditLogRetentionService>();

        return services;
    }
}
