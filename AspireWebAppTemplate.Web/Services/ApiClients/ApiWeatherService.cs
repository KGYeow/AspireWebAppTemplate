using System.Net.Http.Json;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Notifications;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service for the Weather/testing page.
/// Retrieves weather forecasts and provides feature testing utilities (e.g., notification creation).
/// </summary>
public class ApiWeatherService(HttpClient httpClient)
{
    #region Weather

    /// <summary>
    /// Streams weather forecasts from the API and returns up to the specified number of items.
    /// </summary>
    public async Task<WeatherForecast[]> GetWeatherAsync(int maxItems = 10, CancellationToken cancellationToken = default)
    {
        List<WeatherForecast>? forecasts = null;

        await foreach (var forecast in httpClient.GetFromJsonAsAsyncEnumerable<WeatherForecast>("/WeatherForecast", cancellationToken))
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
        var response = await httpClient.PostAsJsonAsync("/WeatherForecast/send-notification", request);
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
