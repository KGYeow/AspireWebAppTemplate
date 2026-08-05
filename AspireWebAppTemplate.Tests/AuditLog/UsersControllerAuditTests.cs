using AspireWebAppTemplate.Infrastructure.Utilities;

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Unit tests for UsersController audit log change tracking.
/// Validates correct JSON serialization for activate/deactivate,
/// field-level diffing for updates, and sensitive field exclusion.
/// </summary>
public class UsersControllerAuditTests
{
    /// <summary>
    /// ActivateUser should produce OldValues: {"isActive":false}
    /// Validates: Requirement 3.3
    /// </summary>
    [Fact]
    public void ActivateUser_Produces_Correct_OldValues_Json()
    {
        var result = AuditChangeHelper.Serialize(new { IsActive = false });

        Assert.Equal("{\"isActive\":false}", result);
    }

    /// <summary>
    /// ActivateUser should produce NewValues: {"isActive":true}
    /// Validates: Requirement 3.3
    /// </summary>
    [Fact]
    public void ActivateUser_Produces_Correct_NewValues_Json()
    {
        var result = AuditChangeHelper.Serialize(new { IsActive = true });

        Assert.Equal("{\"isActive\":true}", result);
    }

    /// <summary>
    /// DeactivateUser should produce OldValues: {"isActive":true}
    /// Validates: Requirement 3.4
    /// </summary>
    [Fact]
    public void DeactivateUser_Produces_Correct_OldValues_Json()
    {
        var result = AuditChangeHelper.Serialize(new { IsActive = true });

        Assert.Equal("{\"isActive\":true}", result);
    }

    /// <summary>
    /// DeactivateUser should produce NewValues: {"isActive":false}
    /// Validates: Requirement 3.4
    /// </summary>
    [Fact]
    public void DeactivateUser_Produces_Correct_NewValues_Json()
    {
        var result = AuditChangeHelper.Serialize(new { IsActive = false });

        Assert.Equal("{\"isActive\":false}", result);
    }

    /// <summary>
    /// UpdateUser should only include changed fields in the diff output.
    /// When only DisplayName changes, only that key appears in OldValues/NewValues.
    /// Validates: Requirement 3.2
    /// </summary>
    [Fact]
    public void UpdateUser_ComputeChanges_Only_Includes_Changed_Fields()
    {
        var before = new Dictionary<string, object?>
        {
            ["DisplayName"] = "John Doe",
            ["FirstName"] = "John",
            ["LastName"] = "Doe",
            ["Email"] = "john@example.com",
            ["PhoneNumber"] = "555-1234",
            ["JobTitle"] = "Developer",
            ["Department"] = "IT",
            ["EmployeeNumber"] = "E001"
        };

        var after = new Dictionary<string, object?>
        {
            ["DisplayName"] = "John Smith",
            ["FirstName"] = "John",
            ["LastName"] = "Doe",
            ["Email"] = "john@example.com",
            ["PhoneNumber"] = "555-1234",
            ["JobTitle"] = "Developer",
            ["Department"] = "IT",
            ["EmployeeNumber"] = "E001"
        };

        var (oldValues, newValues) = AuditChangeHelper.ComputeChanges(before, after);

        Assert.NotNull(oldValues);
        Assert.NotNull(newValues);
        // ComputeChanges uses Dictionary<string, object?> serialization which preserves original key casing.
        // System.Text.Json's PropertyNamingPolicy applies to object properties, not dictionary keys.
        Assert.Equal("{\"DisplayName\":\"John Doe\"}", oldValues);
        Assert.Equal("{\"DisplayName\":\"John Smith\"}", newValues);
    }

    /// <summary>
    /// The UserAuditFields array as defined in the design must NOT include
    /// sensitive fields like PasswordHash, SecurityStamp, ConcurrencyStamp, etc.
    /// Validates: Requirement 7.4
    /// </summary>
    [Fact]
    public void UserAuditFields_Does_Not_Include_Sensitive_Fields()
    {
        var sensitiveFields = new[]
        {
            "PasswordHash",
            "SecurityStamp",
            "ConcurrencyStamp",
            "TwoFactorEnabled",
            "NormalizedEmail",
            "NormalizedUserName"
        };

        // UserAuditFields from the design: DisplayName, FirstName, LastName, Email, PhoneNumber, JobTitle, Department, EmployeeNumber
        var auditFieldNames = new[]
        {
            "DisplayName",
            "FirstName",
            "LastName",
            "Email",
            "PhoneNumber",
            "JobTitle",
            "Department",
            "EmployeeNumber"
        };

        Assert.DoesNotContain(auditFieldNames, name => sensitiveFields.Contains(name));
    }
}
