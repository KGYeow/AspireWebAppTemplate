// Bugfix: scheduler-dependency-cleanup, Property 2: Preservation - API/Web audit-write and infrastructure graph unchanged
using Amazon.BedrockRuntime;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.Ai;
using AspireWebAppTemplate.Application.Features.Announcements;
using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Application.Features.Authentication;
using AspireWebAppTemplate.Application.Features.Email;
using AspireWebAppTemplate.Application.Features.Navigation;
using AspireWebAppTemplate.Application.Features.Notifications;
using AspireWebAppTemplate.Application.Features.PagePermissions;
using AspireWebAppTemplate.Application.Features.Roles;
using AspireWebAppTemplate.Application.Features.Users;
using AspireWebAppTemplate.Infrastructure.Clients;
using AspireWebAppTemplate.Infrastructure.Data;
using AspireWebAppTemplate.Infrastructure.Data.Entities;
using AspireWebAppTemplate.Infrastructure.Extensions;
using AspireWebAppTemplate.Infrastructure.Identity;
using AspireWebAppTemplate.Infrastructure.Services.AuditLog;
using AspireWebAppTemplate.Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Ganss.Xss;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Gen = FsCheck.Fluent.Gen;
using Property = FsCheck.Property;

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Property 2 (Preservation) baseline tests for the Scheduler over-inheritance bugfix.
/// </summary>
/// <remarks>
/// Bugfix: scheduler-dependency-cleanup, Property 2: Preservation - API/Web audit-write and infrastructure graph unchanged.
///
/// These tests follow the observation-first methodology: they run against the UNFIXED code, record the
/// current (baseline) behavior, and assert it. On unfixed code they are EXPECTED TO PASS — the pass
/// confirms exactly which behavior the fix must preserve. After the fix is implemented, the same
/// assertions must continue to pass (with the purge test retargeted to the extracted retention service).
///
/// Preservation Test Case 1 (regression 3.1): <see cref="AuditLogService.LogAsync"/> resolves the user
/// display name identically — null <c>userId</c> → empty string; known user → <c>DisplayName</c> (empty
/// if null); unknown user → the <c>userId</c> string.
///
/// Preservation Test Case 2 (regression 3.3): <see cref="AuditLogRetentionService.PurgeOldEntriesAsync"/>
/// deletes entries older than the <c>AuditLog:RetentionDays</c> cutoff and returns the purged count.
///
/// Preservation Test Case 3 (regression 3.2): <c>AddInfrastructureServices()</c> registers the full
/// feature graph (users, roles, email, notifications, AI/Bedrock, LDAP, announcements, page permissions,
/// navigation, sanitizer, Web callback client, and the HTTP-backed <see cref="ICurrentUserAccessor"/>).
/// </remarks>
public class SchedulerCleanupPreservationTests
{
    #region Display-name resolution seam (Preservation Test Case 1)

    /// <summary>
    /// Distinguishes the three display-name resolution scenarios the fix must preserve.
    /// </summary>
    private enum UserScenario
    {
        /// <summary>The <c>userId</c> is null (system event) — resolves to an empty string.</summary>
        NullUserId,

        /// <summary>The <c>userId</c> maps to an existing user — resolves to their <c>DisplayName</c>.</summary>
        KnownUser,

        /// <summary>The <c>userId</c> maps to no user — resolves to the <c>userId</c> string itself.</summary>
        UnknownUser
    }

    /// <summary>
    /// Creates a SQLite in-memory <see cref="ApplicationDbContext"/> with foreign-key enforcement
    /// disabled (so audit entries can be seeded without a matching <see cref="ApplicationUser"/>).
    /// </summary>
    /// <returns>The context and its owning open connection (both must be disposed by the caller).</returns>
    private static (ApplicationDbContext dbContext, SqliteConnection connection) CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = OFF;";
        command.ExecuteNonQuery();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();
        return (dbContext, connection);
    }

    /// <summary>
    /// Creates a mock <see cref="UserManager{ApplicationUser}"/> with the minimum store setup.
    /// </summary>
    private static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    /// <summary>
    /// Builds an <see cref="AuditLogService"/> over the supplied context and user manager for exercising
    /// the audit-write display-name resolution path.
    /// </summary>
    private static AuditLogService CreateService(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        => new AuditLogService(dbContext, userManager, NullLogger<AuditLogService>.Instance);

    /// <summary>
    /// Builds an <see cref="AuditLogRetentionService"/> over the supplied context with a default
    /// (365-day) retention configuration.
    /// </summary>
    private static AuditLogRetentionService CreateRetentionService(ApplicationDbContext dbContext)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuditLog:RetentionDays"] = "365"
            })
            .Build();

        return new AuditLogRetentionService(dbContext, configuration, NullLogger<AuditLogRetentionService>.Instance);
    }

    /// <summary>
    /// **Validates: Requirements 3.1**
    /// Baseline (observation-first): for any of the three scenarios (null / known / unknown userId),
    /// <see cref="AuditLogService.LogAsync"/> persists a <c>UserDisplayName</c> equal to the documented
    /// resolution — empty string for null userId, the user's <c>DisplayName</c> for a known user, and the
    /// <c>userId</c> string for an unknown user. On UNFIXED code this PASSES, capturing the behavior to preserve.
    /// </summary>
    [Property(MaxTest = 2)]
    public Property DisplayNameResolution_IsUnchanged()
    {
        // Generate all three resolution scenarios plus a display name for the known-user case.
        var gen = Gen.Elements(UserScenario.NullUserId, UserScenario.KnownUser, UserScenario.UnknownUser)
            .SelectMany(scenario =>
                Gen.Elements("Ada Lovelace", "Grace Hopper", string.Empty)
                    .Select(displayName => (scenario, displayName)));

        return Prop.ForAll(Arb.From(gen), ((UserScenario scenario, string displayName) input) =>
        {
            var (dbContext, connection) = CreateDbContext();
            try
            {
                var mockUserManager = CreateMockUserManager();

                string? userId;
                string expectedDisplayName;
                string expectedUserId;

                switch (input.scenario)
                {
                    case UserScenario.NullUserId:
                        userId = null;
                        expectedDisplayName = string.Empty;
                        expectedUserId = string.Empty;
                        break;

                    case UserScenario.KnownUser:
                        userId = Guid.NewGuid().ToString();
                        var user = new ApplicationUser
                        {
                            Id = userId,
                            UserName = $"user_{userId}@test.com",
                            DisplayName = input.displayName
                        };
                        mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
                        // Known user resolves to DisplayName, falling back to empty string when null.
                        expectedDisplayName = input.displayName ?? string.Empty;
                        expectedUserId = userId;
                        break;

                    default: // UnknownUser
                        userId = Guid.NewGuid().ToString();
                        mockUserManager.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync((ApplicationUser?)null);
                        // Unknown user resolves to the userId string itself.
                        expectedDisplayName = userId;
                        expectedUserId = userId;
                        break;
                }

                var service = CreateService(dbContext, mockUserManager.Object);
                var entityId = Guid.NewGuid().ToString();

                service.LogAsync(new AuditLogRequest
                {
                    UserId = userId,
                    ActionType = AuditActionType.LoginSuccess,
                    EntityType = AuditEntityType.System,
                    EntityId = entityId,
                    EntityName = "Preservation Entity",
                    Description = "Preservation baseline"
                }).GetAwaiter().GetResult();

                var entry = dbContext.AuditLogEntries.FirstOrDefault(e => e.EntityId == entityId);

                var matches = entry is not null
                    && entry.UserId == expectedUserId
                    && entry.UserDisplayName == expectedDisplayName;

                return matches.Label(
                    $"Scenario={input.scenario}, ExpectedUserId='{expectedUserId}', " +
                    $"ExpectedDisplayName='{expectedDisplayName}', " +
                    $"ActualUserId='{entry?.UserId}', ActualDisplayName='{entry?.UserDisplayName}'");
            }
            finally
            {
                dbContext.Dispose();
                connection.Dispose();
            }
        });
    }

    #endregion

    #region Purge behavior (Preservation Test Case 2)

    /// <summary>
    /// **Validates: Requirements 3.3**
    /// Baseline (observation-first): <see cref="AuditLogRetentionService.PurgeOldEntriesAsync"/> deletes
    /// only the entries older than the <c>AuditLog:RetentionDays</c> cutoff and returns the number purged.
    /// Seeded with one stale entry (older than the cutoff) and one recent entry (within the window), the
    /// purge returns 1 and leaves 1 behind. The purge behavior is preserved after moving to the extracted
    /// retention service.
    /// </summary>
    [Fact]
    public async Task PurgeOldEntries_DeletesOnlyEntriesOlderThanCutoff()
    {
        var (dbContext, connection) = CreateDbContext();
        try
        {
            const int retentionDays = 365;

            // Stale entry — older than the retention cutoff, so it must be purged.
            SeedEntry(dbContext, DateTime.UtcNow - TimeSpan.FromDays(retentionDays + 10));
            // Recent entry — within the retention window, so it must be preserved.
            SeedEntry(dbContext, DateTime.UtcNow - TimeSpan.FromDays(retentionDays - 10));

            var service = CreateRetentionService(dbContext);

            var purgedCount = await service.PurgeOldEntriesAsync();

            dbContext.ChangeTracker.Clear();
            var remainingCount = await dbContext.AuditLogEntries.CountAsync();

            // Baseline recorded and asserted: exactly the stale entry is purged.
            Assert.Equal(1, purgedCount);
            Assert.Equal(1, remainingCount);
        }
        finally
        {
            dbContext.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// Seeds a single audit log entry with the given timestamp.
    /// </summary>
    private static void SeedEntry(ApplicationDbContext dbContext, DateTime timestamp)
    {
        dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            UserId = string.Empty,
            UserDisplayName = string.Empty,
            ActionType = AuditActionType.LoginSuccess,
            EntityType = AuditEntityType.System,
            EntityId = "preservation",
            EntityName = "preservation",
            Description = "preservation entry",
            Timestamp = timestamp
        });
        dbContext.SaveChanges();
    }

    #endregion

    #region Full infrastructure graph (Preservation Test Case 3)

    /// <summary>
    /// Determines whether a service of the given type is registered in the collection.
    /// </summary>
    private static bool IsRegistered<T>(IServiceCollection services)
        => services.Any(d => d.ServiceType == typeof(T));

    /// <summary>
    /// **Validates: Requirements 3.2**
    /// Baseline (observation-first): <c>AddInfrastructureServices()</c> registers the full feature graph
    /// used by the API/Web hosts. This records the complete registered set that the fix must leave
    /// unchanged — the retention-service extraction is additive to this graph, never a removal.
    /// On UNFIXED code this PASSES, capturing the infrastructure graph to preserve.
    /// </summary>
    [Fact]
    public void AddInfrastructureServices_RegistersFullFeatureGraph()
    {
        var services = new ServiceCollection();

        services.AddInfrastructureServices();

        // Feature services (users, roles, email, notifications, LDAP, announcements, page permissions, navigation).
        Assert.True(IsRegistered<IUserService>(services), "IUserService must remain registered.");
        Assert.True(IsRegistered<IRoleService>(services), "IRoleService must remain registered.");
        Assert.True(IsRegistered<IEmailService>(services), "IEmailService must remain registered.");
        Assert.True(IsRegistered<IEmailSender<ApplicationUser>>(services), "IEmailSender<ApplicationUser> must remain registered.");
        Assert.True(IsRegistered<IEmailTemplateService>(services), "IEmailTemplateService must remain registered.");
        Assert.True(IsRegistered<INotificationService>(services), "INotificationService must remain registered.");
        Assert.True(IsRegistered<IAuthService>(services), "IAuthService must remain registered.");
        Assert.True(IsRegistered<ILoginService>(services), "ILoginService must remain registered.");
        Assert.True(IsRegistered<IRegisterService>(services), "IRegisterService must remain registered.");
        Assert.True(IsRegistered<ILdapAuthService>(services), "ILdapAuthService must remain registered.");
        Assert.True(IsRegistered<ILdapLoginService>(services), "ILdapLoginService must remain registered.");
        Assert.True(IsRegistered<IAnnouncementService>(services), "IAnnouncementService must remain registered.");
        Assert.True(IsRegistered<IPagePermissionService>(services), "IPagePermissionService must remain registered.");
        Assert.True(IsRegistered<INavigationService>(services), "INavigationService must remain registered.");
        Assert.True(IsRegistered<INavigationProvider>(services), "INavigationProvider must remain registered.");

        // Audit log service (the currently Identity-coupled service on the full graph).
        Assert.True(IsRegistered<IAuditLogService>(services), "IAuditLogService must remain registered on the full graph.");

        // AI/Bedrock, sanitizer, Web callback client, and the HTTP-backed current-user accessor.
        Assert.True(IsRegistered<IAiService>(services), "IAiService must remain registered.");
        Assert.True(IsRegistered<AmazonBedrockRuntimeClient>(services), "AmazonBedrockRuntimeClient must remain registered.");
        Assert.True(IsRegistered<HtmlSanitizer>(services), "HtmlSanitizer must remain registered.");
        Assert.True(IsRegistered<WebCallbackClient>(services), "WebCallbackClient must remain registered.");
        Assert.True(IsRegistered<ICurrentUserAccessor>(services), "ICurrentUserAccessor (HTTP-backed) must remain registered.");
    }

    #endregion
}
