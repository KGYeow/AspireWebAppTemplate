using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BlazorWebAppTemplate.Abstractions;
using BlazorWebAppTemplate.Core.Domain.Enums;
using BlazorWebAppTemplate.Core.Utilities;
using BlazorWebAppTemplate.Data;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using MudBlazor;

namespace BlazorWebAppTemplate.Components.Pages.UserManagement;

/// <summary>
/// Dialog for editing a user's profile information.
/// </summary>
public partial class EditUserDialog : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Audit log service for recording user update events.
    /// </summary>
    [Inject] private IAuditLogService AuditLogService { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// The MudBlazor dialog instance.
    /// </summary>
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>
    /// Provides the current authentication state for identifying the acting user.
    /// </summary>
    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    #endregion

    #region Parameters

    /// <summary>
    /// The user's Identity ID to edit.
    /// </summary>
    [Parameter]
    public string UserId { get; set; } = "";

    #endregion

    #region State

    /// <summary>
    /// The form input model.
    /// </summary>
    private InputModel Input { get; set; } = new();

    /// <summary>
    /// Drives the <c>EditForm</c> validation context.
    /// </summary>
    private EditContext editContext = default!;

    /// <summary>
    /// The loaded user entity.
    /// </summary>
    private ApplicationUser? user;

    /// <summary>
    /// Whether the user is being loaded.
    /// </summary>
    protected bool IsLoadingUser { get; private set; } = true;

    /// <summary>
    /// Whether a save operation is in progress.
    /// </summary>
    protected bool IsBusy { get; set; }

    /// <summary>
    /// Error message displayed on failure.
    /// </summary>
    protected string? StatusMessage { get; set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the edit context and loads the user data.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        editContext = new EditContext(Input);

        user = await UserManager.FindByIdAsync(UserId);
        if (user is not null)
        {
            Input.DisplayName = user.DisplayName ?? "";
            Input.FirstName = user.FirstName;
            Input.LastName = user.LastName;
            Input.Email = user.Email ?? "";
            Input.Phone = user.PhoneNumber;
            Input.EmployeeNumber = user.EmployeeNumber;
            Input.JobTitle = user.JobTitle;
            Input.Department = user.Department;
        }

        IsLoadingUser = false;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Saves the updated user profile.
    /// </summary>
    protected async Task OnSubmitAsync()
    {
        if (IsBusy || user is null) return;
        if (!editContext.Validate()) return;

        IsBusy = true;
        StatusMessage = null;

        try
        {
            // Capture old values before applying changes for audit trail
            var oldValues = new Dictionary<string, string?>
            {
                [nameof(user.DisplayName)] = user.DisplayName,
                [nameof(user.FirstName)] = user.FirstName,
                [nameof(user.LastName)] = user.LastName,
                [nameof(user.Email)] = user.Email,
                [nameof(user.PhoneNumber)] = user.PhoneNumber,
                [nameof(user.EmployeeNumber)] = user.EmployeeNumber,
                [nameof(user.JobTitle)] = user.JobTitle,
                [nameof(user.Department)] = user.Department
            };

            user.DisplayName = Input.DisplayName;
            user.FirstName = Input.FirstName;
            user.LastName = Input.LastName;
            user.Email = Input.Email;
            user.PhoneNumber = Input.Phone;
            user.EmployeeNumber = Input.EmployeeNumber;
            user.JobTitle = Input.JobTitle;
            user.Department = Input.Department;
            user.UpdatedUtc = DateTime.UtcNow;

            var result = await UserManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                StatusMessage = string.Join(" ", result.Errors.Select(e => e.Description));
                return;
            }

            // Audit: log user update event with old/new values (fire-and-forget safe)
            try
            {
                var newValues = new Dictionary<string, string?>
                {
                    [nameof(user.DisplayName)] = user.DisplayName,
                    [nameof(user.FirstName)] = user.FirstName,
                    [nameof(user.LastName)] = user.LastName,
                    [nameof(user.Email)] = user.Email,
                    [nameof(user.PhoneNumber)] = user.PhoneNumber,
                    [nameof(user.EmployeeNumber)] = user.EmployeeNumber,
                    [nameof(user.JobTitle)] = user.JobTitle,
                    [nameof(user.Department)] = user.Department
                };

                // Only include fields that actually changed
                var changedOld = new Dictionary<string, string?>();
                var changedNew = new Dictionary<string, string?>();
                foreach (var key in oldValues.Keys)
                {
                    if (!string.Equals(oldValues[key], newValues[key], StringComparison.Ordinal))
                    {
                        changedOld[key] = oldValues[key];
                        changedNew[key] = newValues[key];
                    }
                }

                var authState = await AuthStateTask;
                var actingUserName = authState.User.Identity?.Name;
                string? actingUserId = null;
                if (actingUserName is not null)
                {
                    var actingUser = await UserManager.FindByNameAsync(actingUserName);
                    actingUserId = actingUser?.Id;
                }

                var oldValuesJson = changedOld.Count > 0 ? JsonSerializer.Serialize(changedOld) : null;
                var newValuesJson = changedNew.Count > 0 ? JsonSerializer.Serialize(changedNew) : null;

                await AuditLogService.LogAsync(
                    actingUserId,
                    AuditActionType.UserUpdated,
                    AuditEntityType.User,
                    user.Id,
                    user.DisplayName ?? user.UserName ?? "",
                    $"User '{user.DisplayName ?? user.UserName}' profile updated.",
                    oldValues: oldValuesJson,
                    newValues: newValuesJson);
            }
            catch { /* audit failures must not interrupt the primary operation */ }

            MudDialog.Close(DialogResult.Ok(true));
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Input Model

    /// <summary>
    /// Form model for the edit user dialog.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>The display name.</summary>
        [Required]
        [Display(Name = "Display Name")]
        public string DisplayName { get; set; } = "";

        /// <summary>The first name.</summary>
        [Display(Name = "First Name")]
        public string? FirstName { get; set; }

        /// <summary>The last name.</summary>
        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        /// <summary>The email address.</summary>
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        /// <summary>The phone number.</summary>
        [Display(Name = "Phone")]
        public string? Phone { get; set; }

        /// <summary>The employee number.</summary>
        [OptionalPhone]
        [Display(Name = "Employee Number")]
        public string? EmployeeNumber { get; set; }

        /// <summary>The job title.</summary>
        [Display(Name = "Job Title")]
        public string? JobTitle { get; set; }

        /// <summary>The department.</summary>
        [Display(Name = "Department")]
        public string? Department { get; set; }
    }

    #endregion
}
