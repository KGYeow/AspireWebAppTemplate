using AspireWebAppTemplate.Application.Features.Template.Notifications;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.Web.Components.Pages.Example;

/// <summary>
/// Weather page — demonstrates calling the API service via HTTP client
/// and displaying the results in a MudDataGrid. Also includes feature testing
/// tools for notifications.
/// </summary>
public partial class Weather : ComponentBase
{
    #region Injected Services

    [Inject] private ApiWeatherService WeatherApi { get; set; } = default!;

    #endregion

    #region State — Weather

    private WeatherForecast[]? forecasts;
    private bool isLoading = true;

    #endregion

    #region State — Notification Testing

    /// <summary>
    /// The selected notification category for the test notification.
    /// </summary>
    private NotificationCategory _selectedCategory = NotificationCategory.System;

    /// <summary>
    /// The title for the test notification.
    /// </summary>
    private string _notificationTitle = "Test Notification";

    /// <summary>
    /// The message body for the test notification.
    /// </summary>
    private string _notificationMessage = "This is a test notification sent from the Weather page.";

    /// <summary>
    /// Whether a test notification is currently being sent.
    /// </summary>
    private bool _isSending;

    /// <summary>
    /// Status message displayed after sending a test notification.
    /// </summary>
    private string? _notificationStatus;

    /// <summary>
    /// Severity of the status message (success or error).
    /// </summary>
    private Severity _notificationSeverity = Severity.Success;

    #endregion

    #region Lifecycle

    protected override async Task OnInitializedAsync()
    {
        forecasts = await WeatherApi.GetWeatherAsync();
        isLoading = false;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Sends a test notification to the current user via the Weather API.
    /// </summary>
    private async Task SendTestNotification()
    {
        _isSending = true;
        _notificationStatus = null;

        var request = new CreateNotificationRequest
        {
            Category = _selectedCategory,
            Title = _notificationTitle,
            Message = _notificationMessage
        };

        var result = await WeatherApi.SendNotificationAsync(request);

        if (result.Succeeded)
        {
            _notificationSeverity = Severity.Success;
            _notificationStatus = $"Notification sent! Category: {_selectedCategory}";
        }
        else
        {
            _notificationSeverity = Severity.Error;
            _notificationStatus = $"Error: {result.Error}";
        }

        _isSending = false;
    }

    #endregion
}
