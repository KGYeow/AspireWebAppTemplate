using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.Web.Components.Pages.Example;

/// <summary>
/// Weather page — demonstrates displaying tabular data with MudDataGrid
/// and simulated asynchronous loading.
/// </summary>
public partial class Weather : ComponentBase
{
    private WeatherForecast[]? forecasts;
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        await Task.Delay(500);

        var startDate = DateOnly.FromDateTime(DateTime.Now);
        var summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild",
            "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        forecasts = Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = startDate.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = summaries[Random.Shared.Next(summaries.Length)]
        }).ToArray();

        isLoading = false;
    }

    private class WeatherForecast
    {
        public DateOnly Date { get; set; }
        public int TemperatureC { get; set; }
        public string? Summary { get; set; }
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }
}
