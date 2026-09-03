using AspireWebAppTemplate.Application.Features.Template.Roles;
using AspireWebAppTemplate.Application.Features.Template.Users;

namespace AspireWebAppTemplate.Application.Features.Template.Roles;

/// <summary>
/// Defines the contract for the role management service that handles full role lifecycle
/// operations — CRUD, activation/deactivation, and user-role assignment. All business logic,
/// database access, and audit logging for role operations is encapsulated here.
/// </summary>
/// <remarks>
/// Implementations should be registered as scoped services to align with the per-request
/// <c>DbContext</c> lifetime. Controllers delegate to this service without containing any
/// business logic, RoleManager usage, or ApplicationDbContext queries.
/// </remarks>
public interface IRoleService
{
    /// <summary>
    /// Retrieves all roles in the system, including user counts for each role.
    /// Results are ordered by <see cref="RoleDto.Position"/> ascending.
    /// </summary>
    /// <returns>
    /// A task that resolves to a list of <see cref="RoleDto"/> objects representing
    /// all roles in the system with their current user counts.
    /// </returns>
    Task<List<RoleDto>> GetAllAsync();

    /// <summary>
    /// Retrieves a single role by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the role to retrieve.</param>
    /// <returns>
    /// A task that resolves to a <see cref="RoleDto"/> containing the role's details.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no role exists with the specified <paramref name="id"/>.
    /// </exception>
    Task<RoleDto> GetByIdAsync(string id);

    /// <summary>
    /// Creates a new role in the system using the provided request data.
    /// Performs audit logging for the create operation.
    /// </summary>
    /// <param name="request">
    /// A <see cref="CreateRoleRequest"/> containing the name, display name, description,
    /// position, and active status for the new role.
    /// </param>
    /// <returns>
    /// A task that resolves to a <see cref="RoleDto"/> representing the newly created role.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when identity validation fails (e.g., duplicate role name). The exception
    /// message contains the concatenated identity error descriptions.
    /// </exception>
    Task<RoleDto> CreateAsync(CreateRoleRequest request);

    /// <summary>
    /// Updates an existing role with the provided request data.
    /// Performs audit logging with old/new value change tracking.
    /// </summary>
    /// <param name="id">The unique identifier of the role to update.</param>
    /// <param name="request">
    /// A <see cref="CreateRoleRequest"/> containing the updated role properties.
    /// </param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no role exists with the specified <paramref name="id"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the role is a system role (IsSystem = true) and cannot be modified,
    /// or when identity validation fails (e.g., duplicate role name).
    /// </exception>
    Task UpdateAsync(string id, CreateRoleRequest request);

    /// <summary>
    /// Deletes an existing role from the system.
    /// Performs audit logging for the delete operation.
    /// </summary>
    /// <param name="id">The unique identifier of the role to delete.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no role exists with the specified <paramref name="id"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the role is a system role (IsSystem = true) and cannot be deleted,
    /// or when users are still assigned to the role and must be unassigned first.
    /// </exception>
    Task DeleteAsync(string id);

    /// <summary>
    /// Activates an existing role, setting its IsActive status to true.
    /// Performs audit logging for the activation operation.
    /// </summary>
    /// <param name="id">The unique identifier of the role to activate.</param>
    /// <returns>A task representing the asynchronous activation operation.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no role exists with the specified <paramref name="id"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the role is a system role (IsSystem = true) and cannot be modified.
    /// </exception>
    Task ActivateAsync(string id);

    /// <summary>
    /// Deactivates an existing role, setting its IsActive status to false.
    /// Performs audit logging for the deactivation operation.
    /// </summary>
    /// <param name="id">The unique identifier of the role to deactivate.</param>
    /// <returns>A task representing the asynchronous deactivation operation.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no role exists with the specified <paramref name="id"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the role is a system role (IsSystem = true) and cannot be modified.
    /// </exception>
    Task DeactivateAsync(string id);

    /// <summary>
    /// Assigns one or more users to a role in bulk. Each user assignment is attempted
    /// independently — individual failures do not prevent other assignments from succeeding.
    /// Performs audit logging for each successful assignment.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role to assign users to.</param>
    /// <param name="userIds">An array of user identifiers to assign to the role.</param>
    /// <returns>
    /// A task that resolves to a <see cref="RoleAssignmentResult"/> containing the count
    /// of successful and failed assignments, where Success + Failed equals the total
    /// number of user IDs provided.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no role exists with the specified <paramref name="roleId"/>.
    /// </exception>
    Task<RoleAssignmentResult> AssignUsersAsync(string roleId, string[] userIds);

    /// <summary>
    /// Removes a single user from a role.
    /// Performs audit logging for the unassignment operation.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role to remove the user from.</param>
    /// <param name="userId">The unique identifier of the user to remove from the role.</param>
    /// <returns>A task representing the asynchronous removal operation.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no role exists with the specified <paramref name="roleId"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the role has RequiresMinimumUser set to true and removing this user
    /// would leave zero users assigned to the role.
    /// </exception>
    Task RemoveUserAsync(string roleId, string userId);

    /// <summary>
    /// Retrieves the list of users currently assigned to a specific role.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role whose users are being queried.</param>
    /// <returns>
    /// A task that resolves to a list of <see cref="UserDto"/> objects representing
    /// the users assigned to the specified role.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no role exists with the specified <paramref name="roleId"/>.
    /// </exception>
    Task<List<UserDto>> GetUsersInRoleAsync(string roleId);
}
