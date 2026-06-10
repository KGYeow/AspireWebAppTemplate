using BlazorWebAppTemplate.Core.Domain.Enums;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using BlazorWebAppTemplate.Services;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BlazorWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Property-based tests for Property 2: User display name resolution.
/// Verifies that the <see cref="AuditLogService.LogAsync"/> method correctly resolves
/// the UserDisplayName based on the userId parameter:
/// - Existing user → ApplicationUser.DisplayName
/// - Unknown userId → userId string itself
/// - Null userId → empty string
/// </summary>
/// <remarks>
/// Feature: audit-log, Property 2: User display name resolution
/// </remarks>
public class UserDisplayNameResolutionPropertyTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly SqliteConnection _connection;

    public UserDisplayNameResolutionPropertyTests()
    {
        // Use a shared SQLite in-memory connection that stays open for the lifetime of the test
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Disable foreign key enforcement so we can test display name resolution
        // independently of FK constraints (unknown userId would violate FK otherwise)
        using var command = _connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = OFF;";
        command.ExecuteNonQuery();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// **Validates: Requirements 3.4**
    /// For any log request with a userId matching an existing ApplicationUser,
    /// the persisted UserDisplayName SHALL equal the ApplicationUser.DisplayName.
    /// </summary>
    [Property(MaxTest = 1)]
    public async Task<bool> ExistingUser_DisplayName_IsResolved(NonEmptyString displayName)
    {
        // Arrange: Create a user in the database with a known DisplayName
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"user_{userId}@test.com",
            NormalizedUserName = $"USER_{userId}@TEST.COM",
            Email = $"user_{userId}@test.com",
            NormalizedEmail = $"USER_{userId}@TEST.COM",
            DisplayName = displayName.Get,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Mock UserManager to return the user when FindByIdAsync is called
        var mockUserManager = CreateMockUserManager();
        mockUserManager
            .Setup(m => m.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var service = CreateService(mockUserManager.Object);

        // Act: Log an audit entry with the existing user's ID
        await service.LogAsync(
            userId,
            AuditActionType.UserCreated,
            AuditEntityType.User,
            "entity-1",
            "Test Entity",
            "Test description");

        // Assert: The persisted entry should have the user's DisplayName
        var entry = await _dbContext.AuditLogEntries
            .FirstOrDefaultAsync(e => e.UserId == userId);

        return entry is not null && entry.UserDisplayName == displayName.Get;
    }

    /// <summary>
    /// **Validates: Requirements 3.6**
    /// For any log request with a non-null userId that does not match any ApplicationUser,
    /// the persisted UserDisplayName SHALL equal the userId string itself.
    /// </summary>
    [Property(MaxTest = 1)]
    public async Task<bool> UnknownUserId_DisplayName_IsUserId(NonEmptyString userId)
    {
        // Arrange: Mock UserManager to return null (user not found)
        var userIdValue = userId.Get;
        var mockUserManager = CreateMockUserManager();
        mockUserManager
            .Setup(m => m.FindByIdAsync(userIdValue))
            .ReturnsAsync((ApplicationUser?)null);

        var service = CreateService(mockUserManager.Object);

        // Act: Log an audit entry with an unknown userId
        await service.LogAsync(
            userIdValue,
            AuditActionType.LoginFailed,
            AuditEntityType.System,
            "entity-2",
            "Test Entity",
            "Test description");

        // Assert: The persisted entry should have the userId as UserDisplayName
        var entry = await _dbContext.AuditLogEntries
            .FirstOrDefaultAsync(e => e.UserId == userIdValue);

        return entry is not null && entry.UserDisplayName == userIdValue;
    }

    /// <summary>
    /// **Validates: Requirements 3.5**
    /// For any log request with a null userId,
    /// the persisted UserDisplayName SHALL be an empty string.
    /// </summary>
    [Property(MaxTest = 1)]
    public async Task<bool> NullUserId_DisplayName_IsEmptyString()
    {
        // Arrange: Mock UserManager (won't be called for null userId)
        var mockUserManager = CreateMockUserManager();
        var service = CreateService(mockUserManager.Object);

        // Use a unique entity ID to avoid collisions across test runs
        var entityId = Guid.NewGuid().ToString();

        // Act: Log an audit entry with null userId
        await service.LogAsync(
            null,
            AuditActionType.SettingsChanged,
            AuditEntityType.Settings,
            entityId,
            "System Settings",
            "System event");

        // Assert: The persisted entry should have empty string as UserDisplayName
        var entry = await _dbContext.AuditLogEntries
            .FirstOrDefaultAsync(e => e.EntityId == entityId);

        return entry is not null
            && entry.UserId == string.Empty
            && entry.UserDisplayName == string.Empty;
    }

    /// <summary>
    /// Creates a mock <see cref="UserManager{ApplicationUser}"/> with the minimum required setup.
    /// </summary>
    private static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    /// <summary>
    /// Creates an <see cref="AuditLogService"/> instance with the shared DbContext,
    /// the provided UserManager mock, and default configuration.
    /// </summary>
    private AuditLogService CreateService(UserManager<ApplicationUser> userManager)
    {
        var logger = NullLogger<AuditLogService>.Instance;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuditLog:RetentionDays"] = "365"
            })
            .Build();

        return new AuditLogService(_dbContext, userManager, logger, configuration);
    }
}
