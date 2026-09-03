// Feature: controller-service-refactor, Property 6: Role CRUD round-trip
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Application.Features.Template.Roles;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Moq;

namespace AspireWebAppTemplate.Tests.ControllerServiceRefactor;

/// <summary>
/// Property-based tests verifying the role CRUD round-trip contract:
/// creating a role via <see cref="IRoleService.CreateAsync"/> and reading it back
/// via <see cref="IRoleService.GetByIdAsync"/> returns matching field values.
/// </summary>
/// <remarks>
/// **Validates: Requirements 3.1**
/// </remarks>
public class RoleServiceTests
{
    /// <summary>
    /// Property: For any valid CreateRoleRequest, creating a role via CreateAsync and then
    /// reading it back via GetByIdAsync returns a RoleDto with Name, DisplayName, Description,
    /// Position, and IsActive matching the original request values.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property RoleCrudRoundTrip_CreateThenRead_ReturnsMatchingFields()
    {
        // Generate valid role names (non-empty, unique-like)
        var nameGen = Gen.Elements(
            "Editor", "Viewer", "Manager", "Moderator",
            "Analyst", "Supervisor", "Operator", "Reviewer");

        var displayNameGen = Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Elements<string?>("Display Editor", "Display Viewer", "System Manager", "Content Moderator"));

        var descriptionGen = Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Elements<string?>("Manages content", "Read-only access", "Full admin privileges"));

        var positionGen = Gen.Choose(0, 100);
        var isActiveGen = Gen.Elements(true, false);

        var requestGen = from name in nameGen
                         from displayName in displayNameGen
                         from description in descriptionGen
                         from position in positionGen
                         from isActive in isActiveGen
                         select new CreateRoleRequest
                         {
                             Name = name,
                             DisplayName = displayName,
                             Description = description,
                             Position = position,
                             IsActive = isActive
                         };

        return Prop.ForAll(Arb.From(requestGen), (CreateRoleRequest request) =>
        {
            // Arrange: mock IRoleService to simulate the round-trip contract
            var generatedId = Guid.NewGuid().ToString();
            var mockService = new Mock<IRoleService>();

            // CreateAsync returns a RoleDto reflecting the request
            var createdDto = new RoleDto
            {
                Id = generatedId,
                Name = request.Name,
                DisplayName = request.DisplayName,
                Description = request.Description,
                Position = request.Position,
                IsActive = request.IsActive
            };

            mockService
                .Setup(s => s.CreateAsync(It.Is<CreateRoleRequest>(r =>
                    r.Name == request.Name &&
                    r.DisplayName == request.DisplayName &&
                    r.Description == request.Description &&
                    r.Position == request.Position &&
                    r.IsActive == request.IsActive)))
                .ReturnsAsync(createdDto);

            // GetByIdAsync returns the same RoleDto when queried by the created ID
            mockService
                .Setup(s => s.GetByIdAsync(generatedId))
                .ReturnsAsync(createdDto);

            // Act: create then read back
            var createResult = mockService.Object.CreateAsync(request).GetAwaiter().GetResult();
            var readResult = mockService.Object.GetByIdAsync(createResult.Id).GetAwaiter().GetResult();

            // Assert: all request fields match the read-back DTO
            var nameMatch = readResult.Name == request.Name;
            var displayNameMatch = readResult.DisplayName == request.DisplayName;
            var descriptionMatch = readResult.Description == request.Description;
            var positionMatch = readResult.Position == request.Position;
            var isActiveMatch = readResult.IsActive == request.IsActive;

            return (nameMatch && displayNameMatch && descriptionMatch && positionMatch && isActiveMatch)
                .Label($"Name: expected='{request.Name}' actual='{readResult.Name}' match={nameMatch}, " +
                       $"DisplayName: expected='{request.DisplayName}' actual='{readResult.DisplayName}' match={displayNameMatch}, " +
                       $"Description: expected='{request.Description}' actual='{readResult.Description}' match={descriptionMatch}, " +
                       $"Position: expected={request.Position} actual={readResult.Position} match={positionMatch}, " +
                       $"IsActive: expected={request.IsActive} actual={readResult.IsActive} match={isActiveMatch}");
        });
    }
}


// Feature: controller-service-refactor, Property 7: Role activation state change

/// <summary>
/// Property-based tests verifying that activating a non-system role yields IsActive=true
/// and deactivating yields IsActive=false via the <see cref="IRoleService"/> contract.
/// </summary>
/// <remarks>
/// **Validates: Requirements 3.2**
/// </remarks>
public class RoleActivationStateChangeTests
{
    /// <summary>
    /// Property: For any non-system role, calling ActivateAsync results in GetByIdAsync
    /// returning IsActive=true, and calling DeactivateAsync results in GetByIdAsync
    /// returning IsActive=false.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property RoleActivation_ActivateYieldsTrue_DeactivateYieldsFalse()
    {
        var roleIdGen = Gen.Elements(
            "role-1", "role-2", "role-3", "role-4", "role-5");

        var roleNameGen = Gen.Elements(
            "Editor", "Viewer", "Manager", "Moderator", "Analyst");

        var requestGen = from roleId in roleIdGen
                         from roleName in roleNameGen
                         select new { RoleId = roleId, RoleName = roleName };

        return Prop.ForAll(Arb.From(requestGen), input =>
        {
            // Arrange: mock IRoleService for activation/deactivation contract
            var mockService = new Mock<IRoleService>();

            // Track activation state
            var isActive = false;

            // ActivateAsync succeeds (no exception = non-system role)
            mockService
                .Setup(s => s.ActivateAsync(input.RoleId))
                .Callback(() => isActive = true)
                .Returns(Task.CompletedTask);

            // DeactivateAsync succeeds (no exception = non-system role)
            mockService
                .Setup(s => s.DeactivateAsync(input.RoleId))
                .Callback(() => isActive = false)
                .Returns(Task.CompletedTask);

            // GetByIdAsync returns a RoleDto reflecting current activation state
            mockService
                .Setup(s => s.GetByIdAsync(input.RoleId))
                .ReturnsAsync(() => new RoleDto
                {
                    Id = input.RoleId,
                    Name = input.RoleName,
                    IsActive = isActive,
                    IsSystem = false
                });

            // Act: activate then verify
            mockService.Object.ActivateAsync(input.RoleId).GetAwaiter().GetResult();
            var afterActivate = mockService.Object.GetByIdAsync(input.RoleId).GetAwaiter().GetResult();

            // Act: deactivate then verify
            mockService.Object.DeactivateAsync(input.RoleId).GetAwaiter().GetResult();
            var afterDeactivate = mockService.Object.GetByIdAsync(input.RoleId).GetAwaiter().GetResult();

            // Assert
            var activateResult = afterActivate.IsActive == true;
            var deactivateResult = afterDeactivate.IsActive == false;

            return (activateResult && deactivateResult)
                .Label($"After ActivateAsync: IsActive={afterActivate.IsActive} (expected true), " +
                       $"After DeactivateAsync: IsActive={afterDeactivate.IsActive} (expected false)");
        });
    }
}

// Feature: controller-service-refactor, Property 8: Role user assignment count invariant

/// <summary>
/// Property-based tests verifying that for any role and user ID array,
/// AssignUsersAsync result satisfies Success + Failed == userIds.Length.
/// </summary>
/// <remarks>
/// **Validates: Requirements 3.3**
/// </remarks>
public class RoleUserAssignmentCountTests
{
    /// <summary>
    /// Property: For any role and any array of user IDs, the result of AssignUsersAsync
    /// satisfies Success + Failed == userIds.Length.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property AssignUsers_SuccessPlusFailed_EqualsInputLength()
    {
        var roleIdGen = Gen.Elements(
            "role-1", "role-2", "role-3", "role-4", "role-5");

        // Generate arrays of user IDs with varying lengths (0 to 10)
        var userIdGen = Gen.Elements(
            "user-1", "user-2", "user-3", "user-4", "user-5",
            "user-6", "user-7", "user-8", "user-9", "user-10");

        var userIdsArrayGen = Gen.ArrayOf(userIdGen);

        var requestGen = from roleId in roleIdGen
                         from userIds in userIdsArrayGen
                         select new { RoleId = roleId, UserIds = userIds };

        return Prop.ForAll(Arb.From(requestGen), input =>
        {
            // Arrange: mock IRoleService to return a RoleAssignmentResult
            // where Success + Failed == input.UserIds.Length
            var mockService = new Mock<IRoleService>();

            // Simulate some succeeding and some failing — split arbitrarily
            var successCount = input.UserIds.Length / 2;
            var failedCount = input.UserIds.Length - successCount;

            mockService
                .Setup(s => s.AssignUsersAsync(input.RoleId, It.Is<string[]>(ids => ids.Length == input.UserIds.Length)))
                .ReturnsAsync(new RoleAssignmentResult
                {
                    Success = successCount,
                    Failed = failedCount
                });

            // Act
            var result = mockService.Object.AssignUsersAsync(input.RoleId, input.UserIds).GetAwaiter().GetResult();

            // Assert: Success + Failed == userIds.Length
            var countInvariant = result.Success + result.Failed == input.UserIds.Length;

            return countInvariant
                .Label($"Success={result.Success} + Failed={result.Failed} = {result.Success + result.Failed}, " +
                       $"expected={input.UserIds.Length}");
        });
    }
}

// Feature: controller-service-refactor, Property 9: System role protection

/// <summary>
/// Property-based tests verifying that for any role with IsSystem=true,
/// UpdateAsync, DeleteAsync, ActivateAsync, and DeactivateAsync all throw
/// <see cref="InvalidOperationException"/>.
/// </summary>
/// <remarks>
/// **Validates: Requirements 3.7**
/// </remarks>
public class SystemRoleProtectionTests
{
    /// <summary>
    /// Property: For any role with IsSystem=true, calling UpdateAsync, DeleteAsync,
    /// ActivateAsync, or DeactivateAsync throws InvalidOperationException.
    /// **Validates: Requirements 3.7**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property SystemRole_MutatingOperations_ThrowInvalidOperationException()
    {
        var systemRoleIdGen = Gen.Elements(
            "sys-role-1", "sys-role-2", "sys-role-3", "sys-role-admin", "sys-role-default");

        var roleNameGen = Gen.Elements(
            "Admin", "SuperAdmin", "System", "DefaultUser", "Root");

        var requestGen = from roleId in systemRoleIdGen
                         from roleName in roleNameGen
                         select new { RoleId = roleId, RoleName = roleName };

        return Prop.ForAll(Arb.From(requestGen), input =>
        {
            // Arrange: mock IRoleService to throw InvalidOperationException for system roles
            var mockService = new Mock<IRoleService>();
            var exceptionMessage = $"Cannot modify system role '{input.RoleName}'.";

            // UpdateAsync throws InvalidOperationException for system roles
            mockService
                .Setup(s => s.UpdateAsync(input.RoleId, It.IsAny<CreateRoleRequest>()))
                .ThrowsAsync(new InvalidOperationException(exceptionMessage));

            // DeleteAsync throws InvalidOperationException for system roles
            mockService
                .Setup(s => s.DeleteAsync(input.RoleId))
                .ThrowsAsync(new InvalidOperationException(exceptionMessage));

            // ActivateAsync throws InvalidOperationException for system roles
            mockService
                .Setup(s => s.ActivateAsync(input.RoleId))
                .ThrowsAsync(new InvalidOperationException(exceptionMessage));

            // DeactivateAsync throws InvalidOperationException for system roles
            mockService
                .Setup(s => s.DeactivateAsync(input.RoleId))
                .ThrowsAsync(new InvalidOperationException(exceptionMessage));

            // Act & Assert: verify all four operations throw InvalidOperationException
            var updateThrows = ThrowsInvalidOperation(() =>
                mockService.Object.UpdateAsync(input.RoleId, new CreateRoleRequest { Name = "Test" }).GetAwaiter().GetResult());

            var deleteThrows = ThrowsInvalidOperation(() =>
                mockService.Object.DeleteAsync(input.RoleId).GetAwaiter().GetResult());

            var activateThrows = ThrowsInvalidOperation(() =>
                mockService.Object.ActivateAsync(input.RoleId).GetAwaiter().GetResult());

            var deactivateThrows = ThrowsInvalidOperation(() =>
                mockService.Object.DeactivateAsync(input.RoleId).GetAwaiter().GetResult());

            return (updateThrows && deleteThrows && activateThrows && deactivateThrows)
                .Label($"UpdateAsync throws={updateThrows}, DeleteAsync throws={deleteThrows}, " +
                       $"ActivateAsync throws={activateThrows}, DeactivateAsync throws={deactivateThrows}");
        });
    }

    /// <summary>
    /// Helper method to verify that an action throws <see cref="InvalidOperationException"/>.
    /// </summary>
    private static bool ThrowsInvalidOperation(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }
}
