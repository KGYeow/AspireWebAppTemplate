// Feature: controller-service-refactor, Property 1: CurrentUserAccessor claim extraction round-trip
using System.Net;
using System.Security.Claims;
using AspireWebAppTemplate.ApiService.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Moq;

namespace AspireWebAppTemplate.Tests.ControllerServiceRefactor;

/// <summary>
/// Property-based tests verifying that <see cref="CurrentUserAccessor"/> correctly extracts
/// UserId, UserName, and IpAddress from the HttpContext claims and headers.
/// </summary>
/// <remarks>
/// **Validates: Requirements 1.2**
/// </remarks>
public class CurrentUserAccessorTests
{
    /// <summary>
    /// Property: For any valid UserId, UserName, and IpAddress strings set on an HttpContext,
    /// the CurrentUserAccessor returns those exact values — a round-trip extraction guarantee.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ClaimExtraction_RoundTrip_ReturnsExactValues()
    {
        // Generate non-null, non-empty strings for each identity field
        var nonEmptyStringGen = Gen.Elements(
            "user-123", "admin-456", "test-789", "abc-def-ghi",
            "john.doe", "jane_smith", "System", "12345",
            "192.168.1.1", "10.0.0.1", "::1", "fe80::1");

        var inputGen = from userId in nonEmptyStringGen
                       from userName in nonEmptyStringGen
                       from ipAddress in nonEmptyStringGen
                       select (userId, userName, ipAddress);

        return Prop.ForAll(Arb.From(inputGen), ((string userId, string userName, string ipAddress) input) =>
        {
            // Arrange: build an HttpContext with the given claims and header
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, input.userId),
                new(ClaimTypes.Name, input.userName)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext
            {
                User = principal
            };
            httpContext.Request.Headers["X-Client-Ip"] = input.ipAddress;

            var mockAccessor = new Mock<IHttpContextAccessor>();
            mockAccessor.Setup(a => a.HttpContext).Returns(httpContext);

            // Act
            var sut = new CurrentUserAccessor(mockAccessor.Object);

            // Assert
            var userIdMatch = sut.UserId == input.userId;
            var userNameMatch = sut.UserName == input.userName;
            var ipAddressMatch = sut.IpAddress == input.ipAddress;

            return (userIdMatch && userNameMatch && ipAddressMatch)
                .Label($"UserId: expected='{input.userId}' actual='{sut.UserId}' match={userIdMatch}, " +
                       $"UserName: expected='{input.userName}' actual='{sut.UserName}' match={userNameMatch}, " +
                       $"IpAddress: expected='{input.ipAddress}' actual='{sut.IpAddress}' match={ipAddressMatch}");
        });
    }
}
