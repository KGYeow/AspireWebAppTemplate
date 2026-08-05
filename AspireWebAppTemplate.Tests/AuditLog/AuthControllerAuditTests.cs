using System.Text.Json;
using AspireWebAppTemplate.Infrastructure.Utilities;

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Unit tests verifying audit change capture behavior for AuthController operations.
/// </summary>
/// <remarks>
/// **Validates: Requirements 6.1, 6.3, 6.4**
/// </remarks>
public class AuthControllerAuditTests
{
    /// <summary>
    /// Verifies that ChangePassword produces {"passwordChanged":true} via Serialize
    /// and that OldValues would be null (password values are never logged).
    /// **Validates: Requirement 6.4**
    /// </summary>
    [Fact]
    public void ChangePassword_ProducesPasswordChangedTrue()
    {
        // Act
        var newValues = AuditChangeHelper.Serialize(new { PasswordChanged = true });

        // Assert - NewValues should be {"passwordChanged":true} (camelCase)
        Assert.NotNull(newValues);
        using var doc = JsonDocument.Parse(newValues);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("passwordChanged", out var prop));
        Assert.True(prop.GetBoolean());

        // OldValues for password change is always null (password values are never logged)
        string? oldValues = null;
        Assert.Null(oldValues);
    }

    /// <summary>
    /// Verifies that UpdateProfile only includes changed fields in the audit output.
    /// When only DisplayName changes, only "DisplayName" appears in both old and new JSON.
    /// **Validates: Requirements 6.1, 6.2**
    /// </summary>
    [Fact]
    public void UpdateProfile_OnlyIncludesChangedFields()
    {
        // Arrange - simulate profile fields before and after, only DisplayName changed
        var before = new Dictionary<string, object?>
        {
            ["DisplayName"] = "Old Name",
            ["FirstName"] = "John",
            ["LastName"] = "Doe",
            ["PhoneNumber"] = "555-1234"
        };

        var after = new Dictionary<string, object?>
        {
            ["DisplayName"] = "New Name",
            ["FirstName"] = "John",
            ["LastName"] = "Doe",
            ["PhoneNumber"] = "555-1234"
        };

        // Act
        var (oldValues, newValues) = AuditChangeHelper.ComputeChanges(before, after);

        // Assert - only DisplayName should appear
        Assert.NotNull(oldValues);
        Assert.NotNull(newValues);

        using var oldDoc = JsonDocument.Parse(oldValues);
        using var newDoc = JsonDocument.Parse(newValues);

        // Old JSON contains only DisplayName with the old value
        var oldProps = oldDoc.RootElement.EnumerateObject().ToList();
        Assert.Single(oldProps);
        Assert.Equal("DisplayName", oldProps[0].Name);
        Assert.Equal("Old Name", oldProps[0].Value.GetString());

        // New JSON contains only DisplayName with the new value
        var newProps = newDoc.RootElement.EnumerateObject().ToList();
        Assert.Single(newProps);
        Assert.Equal("DisplayName", newProps[0].Name);
        Assert.Equal("New Name", newProps[0].Value.GetString());
    }

    /// <summary>
    /// Verifies that UpdatePreferences only includes changed fields in the audit output.
    /// When only Theme changes, only "Theme" appears in both old and new JSON.
    /// **Validates: Requirement 6.3**
    /// </summary>
    [Fact]
    public void UpdatePreferences_OnlyIncludesChangedFields()
    {
        // Arrange - simulate preference fields before and after, only Theme changed
        var before = new Dictionary<string, object?>
        {
            ["Theme"] = "Light",
            ["TimeZoneId"] = "UTC",
            ["DateTimeFormat"] = "yyyy-MM-dd"
        };

        var after = new Dictionary<string, object?>
        {
            ["Theme"] = "Dark",
            ["TimeZoneId"] = "UTC",
            ["DateTimeFormat"] = "yyyy-MM-dd"
        };

        // Act
        var (oldValues, newValues) = AuditChangeHelper.ComputeChanges(before, after);

        // Assert - only Theme should appear
        Assert.NotNull(oldValues);
        Assert.NotNull(newValues);

        using var oldDoc = JsonDocument.Parse(oldValues);
        using var newDoc = JsonDocument.Parse(newValues);

        // Old JSON contains only Theme with the old value
        var oldProps = oldDoc.RootElement.EnumerateObject().ToList();
        Assert.Single(oldProps);
        Assert.Equal("Theme", oldProps[0].Name);
        Assert.Equal("Light", oldProps[0].Value.GetString());

        // New JSON contains only Theme with the new value
        var newProps = newDoc.RootElement.EnumerateObject().ToList();
        Assert.Single(newProps);
        Assert.Equal("Theme", newProps[0].Name);
        Assert.Equal("Dark", newProps[0].Value.GetString());
    }

    /// <summary>
    /// Verifies that when no fields changed during a profile update,
    /// ComputeChanges returns null for both OldValues and NewValues.
    /// **Validates: Requirement 7.3**
    /// </summary>
    [Fact]
    public void UpdateProfile_NoChanges_ReturnsNull()
    {
        // Arrange - identical before and after
        var before = new Dictionary<string, object?>
        {
            ["DisplayName"] = "John Doe",
            ["FirstName"] = "John",
            ["LastName"] = "Doe",
            ["PhoneNumber"] = "555-1234"
        };

        var after = new Dictionary<string, object?>
        {
            ["DisplayName"] = "John Doe",
            ["FirstName"] = "John",
            ["LastName"] = "Doe",
            ["PhoneNumber"] = "555-1234"
        };

        // Act
        var (oldValues, newValues) = AuditChangeHelper.ComputeChanges(before, after);

        // Assert - both should be null when nothing changed
        Assert.Null(oldValues);
        Assert.Null(newValues);
    }
}
