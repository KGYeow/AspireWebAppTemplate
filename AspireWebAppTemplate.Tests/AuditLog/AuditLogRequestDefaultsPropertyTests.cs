// Feature: audit-log-old-new-values, Property 6: AuditLogRequest Default Property Values
using AspireWebAppTemplate.Application.Features.Template.AuditLog.Contracts;

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Tests verifying that a newly constructed AuditLogRequest has correct default values.
/// String properties default to string.Empty; nullable properties default to null.
/// </summary>
/// <remarks>
/// **Validates: Requirements 1.2**
/// </remarks>
public class AuditLogRequestDefaultsPropertyTests
{
    /// <summary>
    /// Property: For any newly constructed AuditLogRequest instance where EntityId, EntityName,
    /// and Description are not explicitly assigned, those properties SHALL equal string.Empty.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Fact]
    public void NewInstance_StringProperties_DefaultToEmpty()
    {
        var request = new AuditLogRequest();

        Assert.Equal(string.Empty, request.EntityId);
        Assert.Equal(string.Empty, request.EntityName);
        Assert.Equal(string.Empty, request.Description);
    }

    /// <summary>
    /// Verifies that nullable properties (UserId, OldValues, NewValues, IpAddress)
    /// default to null on a newly constructed AuditLogRequest.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Fact]
    public void NewInstance_NullableProperties_DefaultToNull()
    {
        var request = new AuditLogRequest();

        Assert.Null(request.UserId);
        Assert.Null(request.OldValues);
        Assert.Null(request.NewValues);
        Assert.Null(request.IpAddress);
    }
}
