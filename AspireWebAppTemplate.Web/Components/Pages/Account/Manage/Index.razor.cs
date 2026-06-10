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
using Microsoft.Extensions.Logging;

namespace BlazorWebAppTemplate.Components.Account.Pages.Manage;

/// <summary>
/// Manage account profile page using <c>InteractiveServer</c> render mode.
/// Allows the user to view their username and update their phone number.
/// Uses <see cref="AuthenticationStateProvider"/> instead of <c>HttpContext</c>
/// since <c>HttpContext</c> is not available on a SignalR circuit.
/// </summary>
public partial class Index : ComponentBase
{
    #region Injected Services

    /// <summary>
    /// Manages user accounts.
    /// </summary>
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    /// <summary>
    /// Provides navigation actions.
    /// </summary>
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Structured logger for recording events.
    /// </summary>
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    /// <summary>
    /// Audit log service for recording profile/settings change events.
    /// </summary>
    [Inject] private IAuditLogService AuditLogService { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    /// <summary>
    /// Provides the current authentication state to resolve the user
    /// without depending on <c>HttpContext</c>.
    /// </summary>
    [CascadingParameter]
    private Task<AuthenticationState> AuthStateTask { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// The current user.
    /// </summary>
    private ApplicationUser? user;

    /// <summary>
    /// The user's username (read-only display).
    /// </summary>
    protected string? Username { get; private set; }

    /// <summary>
    /// The user's current phone number from the database.
    /// </summary>
    private string? phoneNumber;

    /// <summary>
    /// The form input model.
    /// </summary>
    private InputModel Input { get; set; } = new();

    /// <summary>
    /// Drives the <c>EditForm</c> validation context.
    /// Initialized synchronously to ensure it is available on the first render.
    /// </summary>
    private EditContext editContext = default!;

    /// <summary>
    /// Status message displayed after save.
    /// </summary>
    protected string? StatusMessage { get; set; }

    /// <summary>
    /// Controls the submit button's disabled state and loading spinner.
    /// </summary>
    protected bool IsBusy { get; private set; }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Initializes the edit context synchronously so the <c>EditForm</c> has a valid
    /// context before the first render.
    /// </summary>
    protected override void OnInitialized()
    {
        editContext = new EditContext(Input);
    }

    /// <summary>
    /// Loads the current user's profile data using <see cref="AuthenticationState"/>.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateTask;
        user = await UserManager.GetUserAsync(authState.User);

        if (user is null)
        {
            NavigationManager.NavigateTo("Account/InvalidUser", forceLoad: true);
            return;
        }

        Username = await UserManager.GetUserNameAsync(user);
        phoneNumber = await UserManager.GetPhoneNumberAsync(user);

        Input.PhoneNumber = phoneNumber;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Saves the updated phone number.
    /// </summary>
    protected async Task OnValidSubmitAsync()
    {
        if (IsBusy || user is null) return;
        IsBusy = true;
        StatusMessage = null;

        try
        {
            if (Input.PhoneNumber != phoneNumber)
            {
                // Capture old values before the change for audit logging
                var oldPhoneNumber = phoneNumber;

                var setPhoneResult = await UserManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Error: Failed to set phone number.";
                    return;
                }

                phoneNumber = Input.PhoneNumber;

                // Audit log: record settings change with old/new values (fire-and-forget safe, won't interrupt primary operation)
                var oldValues = JsonSerializer.Serialize(new { PhoneNumber = oldPhoneNumber });
                var newValues = JsonSerializer.Serialize(new { PhoneNumber = Input.PhoneNumber });

                await AuditLogService.LogAsync(
                    userId: user.Id,
                    actionType: AuditActionType.SettingsChanged,
                    entityType: AuditEntityType.Settings,
                    entityId: user.Id,
                    entityName: user.DisplayName ?? user.UserName ?? user.Id,
                    description: "User updated their profile settings.",
                    oldValues: oldValues,
                    newValues: newValues);
            }

            StatusMessage = "Your profile has been updated.";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error updating profile.");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Input Model

    /// <summary>
    /// Form model for the profile page.
    /// </summary>
    private sealed class InputModel
    {
        /// <summary>
        /// The user's phone number.
        /// </summary>
        [OptionalPhone]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }
    }

    #endregion
}
