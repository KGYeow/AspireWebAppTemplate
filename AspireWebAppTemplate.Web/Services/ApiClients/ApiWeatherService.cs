using System.Net.Http.Json;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Notifications;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service for the Weather/testing page.
/// Retrieves weather forecasts and provides feature testing utilities (e.g., notification creation).
/// </summary>
public class ApiWeatherService
{
    #region Constructor

    /// <summary>
    /// The underlying HttpClient configured with the ApiService base address.
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of <see cref="ApiWeatherService"/> with the configured HttpClient.
    /// </summary>
    /// <param name="http">The HttpClient instance configured via Aspire service discovery.</param>
    public ApiWeatherService(HttpClient http)
    {
        _http = http;
    }

    #endregion

    #region Weather

    /// <summary>
    /// Streams weather forecasts from the API and returns up to the specified number of items.
    /// </summary>
    public async Task<WeatherForecast[]> GetWeatherAsync(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        List<WeatherForecast>? forecasts = null;

        await foreach (var forecast in _http.GetFromJsonAsAsyncEnumerable<WeatherForecast>("/WeatherForecast", cancellationToken))
        {
            if (forecasts?.Count >= maxItems)
            {
                break;
            }
            if (forecast is not null)
            {
                forecasts ??= [];
                forecasts.Add(forecast);
            }
        }

        return forecasts?.ToArray() ?? [];
    }

    #endregion

    #region Notification Testing

    /// <summary>
    /// Sends a notification to the authenticated user via the Weather controller's test endpoint.
    /// Calls POST /WeatherForecast/send-notification.
    /// </summary>
    /// <param name="request">The notification details (category, title, message).</param>
    /// <returns>An <see cref="ApiResult"/> indicating success or failure.</returns>
    public async Task<ApiResult> SendNotificationAsync(CreateNotificationRequest request)
    {
        var response = await _http.PostAsJsonAsync("/WeatherForecast/send-notification", request);
        if (response.IsSuccessStatusCode) return ApiResult.Success();
        return ApiResult.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion
}

/// <summary>
/// Represents a single weather forecast entry.
/// </summary>
public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
