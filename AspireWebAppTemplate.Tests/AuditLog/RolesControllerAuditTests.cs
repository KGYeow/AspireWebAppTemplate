using System.Text.Json;
using AspireWebAppTemplate.ApiService.Utilities;

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Unit tests verifying audit change capture behavior for RolesController scenarios.
/// </summary>
/// <remarks>
/// **Validates: Requirements 4.1, 4.3, 4.4**
/// </remarks>
public class RolesControllerAuditTests
{
    /// <summary>
    /// UpdateRole should only include changed fields in OldValues/NewValues.
    /// When only DisplayName changes, only DisplayName appears in the output.
    /// **Validates: Requirement 4.1**
    /// </summary>
    [Fact]
    public void UpdateRole_OnlyIncludesChangedFields()
    {
        // Arrange: before/after dictionaries where only DisplayName changed
        var before = new Dictionary<string, object?>
        {
            ["Name"] = "admin",
            ["DisplayName"] = "Administrator",
            ["Description"] = "Full access role",
            ["Position"] = 1,
            ["IsActive"] = true
        };

        var after = new Dictionary<string, object?>
        {
            ["Name"] = "admin",
            ["DisplayName"] = "Super Admin",
            ["Description"] = "Full access role",
            ["Position"] = 1,
            ["IsActive"] = true
        };

        // Act
        var (oldValues, newValues) = AuditChangeHelper.ComputeChanges(before, after);

        // Assert: only "DisplayName" appears in the JSON
        Assert.NotNull(oldValues);
        Assert.NotNull(newValues);

        using var oldDoc = JsonDocument.Parse(oldValues);
        using var newDoc = JsonDocument.Parse(newValues);

        var oldKeys = oldDoc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        var newKeys = newDoc.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        Assert.Single(oldKeys);
        Assert.Single(newKeys);
        Assert.Equal("DisplayName", oldKeys[0]);
        Assert.Equal("DisplayName", newKeys[0]);

        Assert.Equal("Administrator", oldDoc.RootElement.GetProperty("DisplayName").GetString());
        Assert.Equal("Super Admin", newDoc.RootElement.GetProperty("DisplayName").GetString());
    }

    /// <summary>
    /// AssignUsersToRole should produce NewValues with camelCase "userIds" array.
    /// **Validates: Requirement 4.3**
    /// </summary>
    [Fact]
    public void AssignUsersToRole_CapturesCorrectNewValuesFormat()
    {
        // Act
        var result = AuditChangeHelper.Serialize(new { UserIds = new[] { "user-1", "user-2" } });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("{\"userIds\":[\"user-1\",\"user-2\"]}", result);
    }

    /// <summary>
    /// RemoveUserFromRole should produce OldValues with camelCase "userId" and "roleName".
    /// **Validates: Requirement 4.4**
    /// </summary>
    [Fact]
    public void RemoveUserFromRole_CapturesCorrectOldValuesFormat()
    {
        // Act
        var result = AuditChangeHelper.Serialize(new { UserId = "user-123", RoleName = "Admin" });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("{\"userId\":\"user-123\",\"roleName\":\"Admin\"}", result);
    }
}
