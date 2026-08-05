// Feature: controller-service-refactor, Property 17: Exception-to-HTTP-status mapping
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.ApiService.Controllers;
using AspireWebAppTemplate.Application.Contracts.Roles;
using AspireWebAppTemplate.Application.Contracts.Users;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AspireWebAppTemplate.Tests.ControllerServiceRefactor;

/// <summary>
/// Property-based tests verifying the exception-to-HTTP-status mapping contract:
/// when a service throws <see cref="KeyNotFoundException"/> the controller returns 404,
/// when it throws <see cref="InvalidOperationException"/> or <see cref="ArgumentException"/>
/// the controller returns 400, and when it succeeds the controller returns the documented
/// success status code (200, 201, or 204).
/// </summary>
/// <remarks>
/// Uses <see cref="RolesController"/> as the representative controller since it exercises
/// all four mapping scenarios across its action methods.
/// **Validates: Requirements 7.1, 7.2, 7.3, 7.4**
/// </remarks>
public class ExceptionMappingTests
{
    /// <summary>
    /// Property: For any role ID, when the service throws <see cref="KeyNotFoundException"/>,
    /// the controller action SHALL return HTTP 404 (NotFoundObjectResult).
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property KeyNotFoundException_Returns404()
    {
        var roleIdGen = Gen.Elements(
            "non-existent-1", "missing-role-2", "gone-3", "absent-4", "unknown-5");

        var messageGen = Gen.Elements(
            "Role not found.", "No role exists with the specified ID.",
            "Role 'xyz' does not exist.", "Entity not found.");

        var inputGen = from roleId in roleIdGen
                       from message in messageGen
                       select new { RoleId = roleId, Message = message };

        return Prop.ForAll(Arb.From(inputGen), input =>
        {
            // Arrange: mock IRoleService to throw KeyNotFoundException
            var mockService = new Mock<IRoleService>();

            mockService
                .Setup(s => s.GetByIdAsync(input.RoleId))
                .ThrowsAsync(new KeyNotFoundException(input.Message));

            mockService
                .Setup(s => s.UpdateAsync(input.RoleId, It.IsAny<CreateRoleRequest>()))
                .ThrowsAsync(new KeyNotFoundException(input.Message));

            mockService
                .Setup(s => s.DeleteAsync(input.RoleId))
                .ThrowsAsync(new KeyNotFoundException(input.Message));

            mockService
                .Setup(s => s.ActivateAsync(input.RoleId))
                .ThrowsAsync(new KeyNotFoundException(input.Message));

            var controller = new RolesController(mockService.Object);

            // Act: call multiple actions and verify all return 404
            var getResult = controller.GetRole(input.RoleId).GetAwaiter().GetResult().Result;
            var updateResult = controller.UpdateRole(input.RoleId, new CreateRoleRequest { Name = "Test" })
                .GetAwaiter().GetResult();
            var deleteResult = controller.DeleteRole(input.RoleId).GetAwaiter().GetResult();
            var activateResult = controller.ActivateRole(input.RoleId).GetAwaiter().GetResult();

            // Assert: all should be NotFoundObjectResult (404)
            var getIs404 = getResult is NotFoundObjectResult;
            var updateIs404 = updateResult is NotFoundObjectResult;
            var deleteIs404 = deleteResult is NotFoundObjectResult;
            var activateIs404 = activateResult is NotFoundObjectResult;

            return (getIs404 && updateIs404 && deleteIs404 && activateIs404)
                .Label($"GetRole 404={getIs404}, UpdateRole 404={updateIs404}, " +
                       $"DeleteRole 404={deleteIs404}, ActivateRole 404={activateIs404}");
        });
    }

    /// <summary>
    /// Property: For any role ID, when the service throws <see cref="InvalidOperationException"/>,
    /// the controller action SHALL return HTTP 400 (BadRequestObjectResult).
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property InvalidOperationException_Returns400()
    {
        var roleIdGen = Gen.Elements(
            "sys-role-1", "system-admin", "protected-3", "locked-4", "immutable-5");

        var messageGen = Gen.Elements(
            "Cannot modify system role.", "Role has assigned users.",
            "Duplicate role name.", "Operation not permitted.");

        var inputGen = from roleId in roleIdGen
                       from message in messageGen
                       select new { RoleId = roleId, Message = message };

        return Prop.ForAll(Arb.From(inputGen), input =>
        {
            // Arrange: mock IRoleService to throw InvalidOperationException
            var mockService = new Mock<IRoleService>();

            mockService
                .Setup(s => s.CreateAsync(It.IsAny<CreateRoleRequest>()))
                .ThrowsAsync(new InvalidOperationException(input.Message));

            mockService
                .Setup(s => s.UpdateAsync(input.RoleId, It.IsAny<CreateRoleRequest>()))
                .ThrowsAsync(new InvalidOperationException(input.Message));

            mockService
                .Setup(s => s.DeleteAsync(input.RoleId))
                .ThrowsAsync(new InvalidOperationException(input.Message));

            mockService
                .Setup(s => s.DeactivateAsync(input.RoleId))
                .ThrowsAsync(new InvalidOperationException(input.Message));

            var controller = new RolesController(mockService.Object);

            // Act
            var createResult = controller.CreateRole(new CreateRoleRequest { Name = "Test" })
                .GetAwaiter().GetResult().Result;
            var updateResult = controller.UpdateRole(input.RoleId, new CreateRoleRequest { Name = "Test" })
                .GetAwaiter().GetResult();
            var deleteResult = controller.DeleteRole(input.RoleId).GetAwaiter().GetResult();
            var deactivateResult = controller.DeactivateRole(input.RoleId).GetAwaiter().GetResult();

            // Assert: all should be BadRequestObjectResult (400)
            var createIs400 = createResult is BadRequestObjectResult;
            var updateIs400 = updateResult is BadRequestObjectResult;
            var deleteIs400 = deleteResult is BadRequestObjectResult;
            var deactivateIs400 = deactivateResult is BadRequestObjectResult;

            return (createIs400 && updateIs400 && deleteIs400 && deactivateIs400)
                .Label($"CreateRole 400={createIs400}, UpdateRole 400={updateIs400}, " +
                       $"DeleteRole 400={deleteIs400}, DeactivateRole 400={deactivateIs400}");
        });
    }

    /// <summary>
    /// Property: For any role ID, when the service throws <see cref="ArgumentException"/>,
    /// the controller action SHALL return HTTP 400 (BadRequestObjectResult).
    /// **Validates: Requirements 7.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ArgumentException_Returns400()
    {
        var roleIdGen = Gen.Elements(
            "role-arg-1", "role-arg-2", "role-arg-3", "role-arg-4", "role-arg-5");

        var messageGen = Gen.Elements(
            "Name cannot be empty.", "Invalid role position.",
            "Description too long.", "Invalid character in name.");

        var inputGen = from roleId in roleIdGen
                       from message in messageGen
                       select new { RoleId = roleId, Message = message };

        return Prop.ForAll(Arb.From(inputGen), input =>
        {
            // Arrange: mock IRoleService to throw ArgumentException
            var mockService = new Mock<IRoleService>();

            mockService
                .Setup(s => s.CreateAsync(It.IsAny<CreateRoleRequest>()))
                .ThrowsAsync(new ArgumentException(input.Message));

            mockService
                .Setup(s => s.UpdateAsync(input.RoleId, It.IsAny<CreateRoleRequest>()))
                .ThrowsAsync(new ArgumentException(input.Message));

            mockService
                .Setup(s => s.DeleteAsync(input.RoleId))
                .ThrowsAsync(new ArgumentException(input.Message));

            mockService
                .Setup(s => s.ActivateAsync(input.RoleId))
                .ThrowsAsync(new ArgumentException(input.Message));

            var controller = new RolesController(mockService.Object);

            // Act
            var createResult = controller.CreateRole(new CreateRoleRequest { Name = "Test" })
                .GetAwaiter().GetResult().Result;
            var updateResult = controller.UpdateRole(input.RoleId, new CreateRoleRequest { Name = "Test" })
                .GetAwaiter().GetResult();
            var deleteResult = controller.DeleteRole(input.RoleId).GetAwaiter().GetResult();
            var activateResult = controller.ActivateRole(input.RoleId).GetAwaiter().GetResult();

            // Assert: all should be BadRequestObjectResult (400)
            var createIs400 = createResult is BadRequestObjectResult;
            var updateIs400 = updateResult is BadRequestObjectResult;
            var deleteIs400 = deleteResult is BadRequestObjectResult;
            var activateIs400 = activateResult is BadRequestObjectResult;

            return (createIs400 && updateIs400 && deleteIs400 && activateIs400)
                .Label($"CreateRole 400={createIs400}, UpdateRole 400={updateIs400}, " +
                       $"DeleteRole 400={deleteIs400}, ActivateRole 400={activateIs400}");
        });
    }

    /// <summary>
    /// Property: For any successful service operation, the controller SHALL return the
    /// documented success status code: 200 (OkObjectResult), 201 (CreatedAtActionResult),
    /// or 204 (NoContentResult).
    /// **Validates: Requirements 7.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property SuccessfulOperation_ReturnsCorrectSuccessCode()
    {
        var roleIdGen = Gen.Elements(
            "role-ok-1", "role-ok-2", "role-ok-3", "role-ok-4", "role-ok-5");

        var roleNameGen = Gen.Elements("Editor", "Viewer", "Manager", "Moderator", "Analyst");

        var inputGen = from roleId in roleIdGen
                       from roleName in roleNameGen
                       select new { RoleId = roleId, RoleName = roleName };

        return Prop.ForAll(Arb.From(inputGen), input =>
        {
            // Arrange: mock IRoleService to succeed on all operations
            var mockService = new Mock<IRoleService>();

            // GetAllAsync returns a list → 200
            mockService
                .Setup(s => s.GetAllAsync())
                .ReturnsAsync(new List<RoleDto> { new RoleDto { Id = input.RoleId, Name = input.RoleName } });

            // GetByIdAsync returns a single role → 200
            mockService
                .Setup(s => s.GetByIdAsync(input.RoleId))
                .ReturnsAsync(new RoleDto { Id = input.RoleId, Name = input.RoleName });

            // CreateAsync returns the created role → 201
            mockService
                .Setup(s => s.CreateAsync(It.IsAny<CreateRoleRequest>()))
                .ReturnsAsync(new RoleDto { Id = input.RoleId, Name = input.RoleName });

            // UpdateAsync completes → 204
            mockService
                .Setup(s => s.UpdateAsync(input.RoleId, It.IsAny<CreateRoleRequest>()))
                .Returns(Task.CompletedTask);

            // DeleteAsync completes → 204
            mockService
                .Setup(s => s.DeleteAsync(input.RoleId))
                .Returns(Task.CompletedTask);

            // ActivateAsync completes → 204
            mockService
                .Setup(s => s.ActivateAsync(input.RoleId))
                .Returns(Task.CompletedTask);

            // GetUsersInRoleAsync returns users → 200
            mockService
                .Setup(s => s.GetUsersInRoleAsync(input.RoleId))
                .ReturnsAsync(new List<UserDto>());

            // AssignUsersAsync returns result → 200
            mockService
                .Setup(s => s.AssignUsersAsync(input.RoleId, It.IsAny<string[]>()))
                .ReturnsAsync(new RoleAssignmentResult { Success = 1, Failed = 0 });

            var controller = new RolesController(mockService.Object);

            // Act & Assert: GetRoles → 200
            var getAllResult = controller.GetRoles().GetAwaiter().GetResult().Result;
            var getAllIs200 = getAllResult is OkObjectResult;

            // GetRole → 200
            var getResult = controller.GetRole(input.RoleId).GetAwaiter().GetResult().Result;
            var getIs200 = getResult is OkObjectResult;

            // CreateRole → 201
            var createResult = controller.CreateRole(new CreateRoleRequest { Name = input.RoleName })
                .GetAwaiter().GetResult().Result;
            var createIs201 = createResult is CreatedAtActionResult;

            // UpdateRole → 200
            var updateResult = controller.UpdateRole(input.RoleId, new CreateRoleRequest { Name = input.RoleName })
                .GetAwaiter().GetResult();
            var updateIs200 = updateResult is OkResult;

            // DeleteRole → 200
            var deleteResult = controller.DeleteRole(input.RoleId).GetAwaiter().GetResult();
            var deleteIs200 = deleteResult is OkResult;

            // ActivateRole → 200
            var activateResult = controller.ActivateRole(input.RoleId).GetAwaiter().GetResult();
            var activateIs200 = activateResult is OkResult;

            // GetUsersInRole → 200
            var getUsersResult = controller.GetUsersInRole(input.RoleId).GetAwaiter().GetResult().Result;
            var getUsersIs200 = getUsersResult is OkObjectResult;

            // AssignUsersToRole → 200
            var assignResult = controller.AssignUsersToRole(input.RoleId, new[] { "user-1" })
                .GetAwaiter().GetResult();
            var assignIs200 = assignResult is OkObjectResult;

            return (getAllIs200 && getIs200 && createIs201 && updateIs200 &&
                    deleteIs200 && activateIs200 && getUsersIs200 && assignIs200)
                .Label($"GetAll 200={getAllIs200}, Get 200={getIs200}, Create 201={createIs201}, " +
                       $"Update 200={updateIs200}, Delete 200={deleteIs200}, Activate 200={activateIs200}, " +
                       $"GetUsers 200={getUsersIs200}, Assign 200={assignIs200}");
        });
    }
}
