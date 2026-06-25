using System.Text.Json;
using AspireWebAppTemplate.ApiService.Utilities;
using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Tests.PagePermissions;

/// <summary>
/// Unit tests verifying the serialization format used in PagePermissionsController
/// for audit log OldValues/NewValues. Also verifies the correct enum values exist.
/// </summary>
public class PagePermissionsControllerAuditTests
{
    /// <summary>
    /// Verify that AuditChangeHelper.Serialize produces JSON with a camelCase "pagePaths"
    /// array property containing the expected values.
    /// </summary>
    [Fact]
    public void PagePermissions_SerializesPagePathsCorrectly()
    {
        // Arrange
        var paths = new[] { "/admin", "/dashboard" };

        // Act
        var json = AuditChangeHelper.Serialize(new { PagePaths = paths });

        // Assert
        Assert.NotNull(json);
        using var doc = JsonDocument.Parse(json!);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("pagePaths", out var pagePathsElement));
        Assert.Equal(JsonValueKind.Array, pagePathsElement.ValueKind);
        Assert.Equal(2, pagePathsElement.GetArrayLength());
        Assert.Equal("/admin", pagePathsElement[0].GetString());
        Assert.Equal("/dashboard", pagePathsElement[1].GetString());
    }

    /// <summary>
    /// Verify that serializing previous paths produces valid JSON with the pagePaths
    /// property as an array.
    /// </summary>
    [Fact]
    public void PagePermissions_OldValuesContainsPagePathsArray()
    {
        // Arrange — simulate old permissions for a role
        var oldPaths = new[] { "/account/settings", "/users" };

        // Act
        var oldValues = AuditChangeHelper.Serialize(new { PagePaths = oldPaths });

        // Assert
        Assert.NotNull(oldValues);
        using var doc = JsonDocument.Parse(oldValues!);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("pagePaths", out var pagePathsElement));
        Assert.Equal(JsonValueKind.Array, pagePathsElement.ValueKind);
        Assert.Equal(2, pagePathsElement.GetArrayLength());
        Assert.Equal("/account/settings", pagePathsElement[0].GetString());
        Assert.Equal("/users", pagePathsElement[1].GetString());
    }

    /// <summary>
    /// Verify that serializing new paths produces valid JSON with the pagePaths
    /// property as an array.
    /// </summary>
    [Fact]
    public void PagePermissions_NewValuesContainsPagePathsArray()
    {
        // Arrange — simulate new permissions for a role
        var newPaths = new[] { "/account/settings", "/users", "/reports" };

        // Act
        var newValues = AuditChangeHelper.Serialize(new { PagePaths = newPaths });

        // Assert
        Assert.NotNull(newValues);
        using var doc = JsonDocument.Parse(newValues!);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("pagePaths", out var pagePathsElement));
        Assert.Equal(JsonValueKind.Array, pagePathsElement.ValueKind);
        Assert.Equal(3, pagePathsElement.GetArrayLength());
        Assert.Equal("/account/settings", pagePathsElement[0].GetString());
        Assert.Equal("/users", pagePathsElement[1].GetString());
        Assert.Equal("/reports", pagePathsElement[2].GetString());
    }

    /// <summary>
    /// Compile-time verification that AuditActionType.SettingsChanged and
    /// AuditEntityType.Role exist and can be assigned to the correct enum types.
    /// </summary>
    [Fact]
    public void PagePermissions_UsesCorrectActionTypeAndEntityType()
    {
        // These assignments verify the enum values exist at compile time.
        // At runtime, verify they have the expected string representation.
        AuditActionType actionType = AuditActionType.SettingsChanged;
        AuditEntityType entityType = AuditEntityType.Role;

        Assert.Equal("SettingsChanged", actionType.ToString());
        Assert.Equal("Role", entityType.ToString());
    }
}
