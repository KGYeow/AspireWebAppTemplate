using AspireWebAppTemplate.ApiService.Abstractions;
using AspireWebAppTemplate.Core.Contracts.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspireWebAppTemplate.ApiService.Controllers;

/// <summary>
/// Example controller for the Weather/testing page.
/// Provides weather forecast data and feature testing utilities (e.g., notification creation).
/// </summary>
[Route("[controller]")]
[Authorize]
public class WeatherForecastController : BaseController
{
    #region Constructor

    private readonly INotificationService _notificationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeatherForecastController"/> class.
    /// </summary>
    /// <param name="notificationService">The notification service for creating test notifications.</param>
    public WeatherForecastController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    #endregion

    #region Weather

    private static readonly string[] Summaries =
        ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

    /// <summary>
    /// Returns a list of sample weather forecasts.
    /// </summary>
    [HttpGet]
    public WeatherForecast[] Get()
    {
        return Enumerable.Range(1, 5).Select(index =>
            new WeatherForecast
            (
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                Summaries[Random.Shared.Next(Summaries.Length)]
            ))
            .ToArray();
    }

    #endregion

    #region Notification Testing

    /// <summary>
    /// Creates a notification for the authenticated user.
    /// Used by the Weather/testing page to exercise the notification system.
    /// </summary>
    /// <param name="request">The notification details (category, title, message).</param>
    /// <returns>200 OK on success.</returns>
    [HttpPost("send-notification")]
    public async Task<IActionResult> SendNotification([FromBody] CreateNotificationRequest request)
    {
        // Override UserId with the current user to ensure notifications go to the caller
        request.UserId = CurrentUserId!;

        await _notificationService.CreateNotificationAsync(request);
        return Ok();
    }

    #endregion
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
