using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.UI.Components.DataGrid;

/// <summary>
/// Reusable dropdown filter component for string columns in <see cref="MudDataGrid{T}"/>
/// where the valid values are a known, finite set of options.
/// Renders a <see cref="MudSelect{T}"/> with the provided option strings.
/// </summary>
/// <typeparam name="T">The data item type of the parent <see cref="MudDataGrid{T}"/>.</typeparam>
/// <remarks>
/// <para>Usage inside a <c>PropertyColumn</c>'s <c>FilterTemplate</c>:</para>
/// <code>
/// &lt;PropertyColumn Property="a =&gt; a.Status" Title="Status" Filterable="true"&gt;
///     &lt;FilterTemplate&gt;
///         &lt;StringFilterSelect T="AnnouncementDto"
///                             FilterContext="context"
///                             DataGrid="dataGrid"
///                             Options="@(new[] { "Active", "Scheduled", "Expired", "Draft" })" /&gt;
///     &lt;/FilterTemplate&gt;
/// &lt;/PropertyColumn&gt;
/// </code>
/// </remarks>
public partial class StringFilterSelect<T> : ComponentBase
{
    #region Parameters

    /// <summary>
    /// The filter context passed from the <c>FilterTemplate</c> of a <see cref="MudDataGrid{T}"/> column.
    /// </summary>
    [Parameter]
    public FilterContext<T> FilterContext { get; set; } = default!;

    /// <summary>
    /// Reference to the parent <see cref="MudDataGrid{T}"/> for triggering server-side reload.
    /// May be null during initial render; filter changes are still applied via FilterContext.
    /// </summary>
    [Parameter]
    public MudDataGrid<T>? DataGrid { get; set; }

    /// <summary>
    /// The set of valid option strings to display in the dropdown.
    /// </summary>
    [Parameter]
    public string[] Options { get; set; } = [];

    /// <summary>
    /// Placeholder text shown when no filter is selected. Defaults to "All".
    /// </summary>
    [Parameter]
    public string Placeholder { get; set; } = "All";

    #endregion

    #region Methods

    /// <summary>
    /// Gets the current display value from the filter definition.
    /// </summary>
    /// <returns>The selected option string, or <c>null</c> if no filter is active.</returns>
    private string? GetValue()
    {
        return FilterContext.FilterDefinition?.Value as string;
    }

    /// <summary>
    /// Sets the filter value from the dropdown selection and triggers grid reload.
    /// Uses the "equals" operator for exact string matching.
    /// </summary>
    /// <param name="value">The selected option, or <c>null</c> to clear the filter.</param>
    private async Task SetValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            FilterContext.FilterDefinition!.Value = null;
            FilterContext.FilterDefinition.Operator = null;
        }
        else
        {
            FilterContext.FilterDefinition!.Value = value;
            FilterContext.FilterDefinition.Operator = FilterOperator.String.Equal;
        }

        if (DataGrid is not null)
            await DataGrid.ReloadServerData();
    }

    #endregion
}
