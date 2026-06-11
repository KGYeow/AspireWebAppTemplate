using AspireWebAppTemplate.Web.Services;
using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.Web.Components.Pages.Example;

/// <summary>
/// Weather page — demonstrates calling the API service via HTTP client
/// and displaying the results in a MudDataGrid.
/// </summary>
public partial class Weather : ComponentBase
{
    [Inject] private ApiWeatherService WeatherApi { get; set; } = default!;

    private WeatherForecast[]? forecasts;
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        forecasts = await WeatherApi.GetWeatherAsync();
        isLoading = false;
    }
}
