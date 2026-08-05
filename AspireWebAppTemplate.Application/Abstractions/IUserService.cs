using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Contracts.Roles;
using AspireWebAppTemplate.Application.Contracts.Users;

namespace AspireWebAppTemplate.Application.Abstractions;

/// <summary>
/// Defines the contract for user management business logic including CRUD operations,
/// search/pagination, activation/deactivation, role assignment, and LDAP synchronization.
/// All database access for user management is encapsulated here — controllers delegate
/// to this service without touching DbContext, UserManager, or RoleManager.
/// </summary>
/// <remarks>
/// Implementations should be registered as scoped services to align with the per-request
/// <c>DbContext</c> lifetime. The service uses <see cref="ICurrentUserAccessor"/> for
/// audit logging and self-operation protection (e.g., preventing self-deletion).
/// </remarks>
public interface IUserService
{
    /// <summary>
    /// Searches and paginates users with an optional filter term matching against
    /// username, display name, email, first name, last name, and department fields.
    /// Results are ordered by display name ascending.
    /// </summary>
    /// <param name="page">The zero-based page index. Defaults to 0 if null.</param>
    /// <param name="pageSize">The maximum number of items per page. Defaults to a system default if null.</param>
    /// <param name="searchTerm">
    /// An optional search term for case-insensitive partial matching against user fields.
    /// When null or empty, all users are returned (paginated).
    /// </param>
    /// <returns>
    /// A task that resolves to a <see cref="PagedResult{T}"/> of <see cref="UserDto"/>
    /// containing the matching users and total count metadata.
    /// </returns>
    Task<PagedResult<UserDto>> SearchAsync(int? page, int? pageSize, string? searchTerm);

    /// <summary>
    /// Retrieves a single user by their unique identifier, including roles and all profile fields.
    /// </summary>
    /// <param name="id">The unique identifier of the user to retrieve.</param>
    /// <returns>
    /// A task that resolves to a <see cref="UserDto"/> containing the user's full profile.
    /// </returns>
    /// <exception cref="KeyNotFoundException">Thrown when no user exists with the specified ID.</exception>
    Task<UserDto> GetByIdAsync(string id);

    /// <summary>
    /// Creates a new user account with the specified email, display name, password, and optional role.
    /// </summary>
    /// <param name="request">
    /// A <see cref="CreateUserRequest"/> containing the email, display name, password,
    /// and optional role assignment for the new user.
    /// </param>
    /// <returns>
    /// A task that resolves to a <see cref="UserDto"/> representing the newly created user.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when identity validation fails (e.g., duplicate email, password policy violation).
    /// </exception>
    Task<UserDto> CreateAsync(CreateUserRequest request);

    /// <summary>
    /// Updates an existing user's profile information (display name, email, phone, etc.).
    /// </summary>
    /// <param name="id">The unique identifier of the user to update.</param>
    /// <param name="request">
    /// An <see cref="UpdateUserRequest"/> containing the fields to update.
    /// </param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no user exists with the specified ID.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when identity validation fails (e.g., duplicate email).
    /// </exception>
    Task UpdateAsync(string id, UpdateUserRequest request);

    /// <summary>
    /// Permanently deletes a user account. Cannot delete the currently authenticated user
    /// or the last active administrator.
    /// </summary>
    /// <param name="id">The unique identifier of the user to delete.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no user exists with the specified ID.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when attempting to delete the currently authenticated user or the last active administrator.
    /// </exception>
    Task DeleteAsync(string id);

    /// <summary>
    /// Activates a user account, allowing the user to sign in.
    /// </summary>
    /// <param name="id">The unique identifier of the user to activate.</param>
    /// <returns>A task representing the asynchronous activation operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no user exists with the specified ID.</exception>
    Task ActivateAsync(string id);

    /// <summary>
    /// Deactivates a user account, preventing the user from signing in.
    /// Cannot deactivate the currently authenticated user.
    /// </summary>
    /// <param name="id">The unique identifier of the user to deactivate.</param>
    /// <returns>A task representing the asynchronous deactivation operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no user exists with the specified ID.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when attempting to deactivate the currently authenticated user.
    /// </exception>
    Task DeactivateAsync(string id);

    /// <summary>
    /// Resets a user's password to a new value specified by an administrator.
    /// Generates a reset token internally and applies the new password.
    /// Notifies the affected user via in-app notification.
    /// </summary>
    /// <param name="id">The unique identifier of the user whose password is being reset.</param>
    /// <param name="newPassword">The new password to set for the user.</param>
    /// <returns>A task representing the asynchronous password reset operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no user exists with the specified ID.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the new password fails identity validation (policy violation).
    /// </exception>
    Task ResetPasswordAsync(string id, string newPassword);

    /// <summary>
    /// Replaces all existing role assignments for the specified user with the provided set of role names.
    /// Removes any roles not in the new set and adds any roles not currently assigned.
    /// </summary>
    /// <param name="id">The unique identifier of the user whose roles are being updated.</param>
    /// <param name="roleNames">
    /// The complete set of role names to assign. An empty array removes all role assignments.
    /// </param>
    /// <returns>A task representing the asynchronous role assignment operation.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no user exists with the specified ID.</exception>
    Task SetRolesAsync(string id, string[] roleNames);

    /// <summary>
    /// Retrieves metadata for all roles in the system, used to populate role selection UI
    /// in user management screens.
    /// </summary>
    /// <returns>
    /// A task that resolves to a list of <see cref="RoleDto"/> containing role metadata
    /// (ID, name, display name, active status).
    /// </returns>
    Task<List<RoleDto>> GetRolesMetadataAsync();

    /// <summary>
    /// Creates a new user account from LDAP/Active Directory attributes. The user is created
    /// with the LDAP auth source and no local password.
    /// </summary>
    /// <param name="attributes">
    /// A <see cref="LdapUserAttributes"/> object containing the user's directory attributes
    /// (display name, email, NTID, department, etc.).
    /// </param>
    /// <returns>
    /// A task that resolves to a <see cref="UserDto"/> representing the newly created LDAP user.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a user with the same username or email already exists in the system.
    /// </exception>
    Task<UserDto> CreateLdapUserAsync(LdapUserAttributes attributes);

    /// <summary>
    /// Performs a bulk synchronization of all LDAP-sourced users by refreshing their profile
    /// attributes from the directory. Returns a streaming-compatible sequence of per-user
    /// progress items for real-time progress reporting.
    /// </summary>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> of <see cref="LdapSyncProgressItem"/> objects,
    /// each containing the total user count, current index, username, and whether the user
    /// was updated, unchanged, or failed.
    /// </returns>
    IAsyncEnumerable<LdapSyncProgressItem> SyncLdapUsersAsync();
}
