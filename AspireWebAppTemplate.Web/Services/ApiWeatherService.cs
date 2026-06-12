using System.Net.Http.Json;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service that retrieves weather forecasts from the API endpoint.
/// </summary>
public class ApiWeatherService(HttpClient httpClient)
{
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
}

/// <summary>
/// Represents a single weather forecast entry.
/// </summary>
public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
