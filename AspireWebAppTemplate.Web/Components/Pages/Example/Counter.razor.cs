using Microsoft.AspNetCore.Components;

namespace AspireWebAppTemplate.Web.Components.Pages.Example;

/// <summary>
/// Counter page — demonstrates basic Blazor interactivity with a click counter.
/// </summary>
public partial class Counter : ComponentBase
{
    private int currentCount;

    private void IncrementCount()
    {
        currentCount++;
    }
}
