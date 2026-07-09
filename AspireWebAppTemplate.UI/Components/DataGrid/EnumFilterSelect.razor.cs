using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AspireWebAppTemplate.UI.Components.DataGrid;

/// <summary>
/// Reusable dropdown filter component for enum-backed string columns in <see cref="MudDataGrid{T}"/>.
/// Renders a <see cref="MudSelect{T}"/> with options derived from the enum values.
/// Works with columns that store enum values as their <c>ToString()</c> representation.
/// </summary>
/// <typeparam name="T">The data item type of the parent <see cref="MudDataGrid{T}"/>.</typeparam>
/// <typeparam name="TEnum">The enum type whose values populate the dropdown options.</typeparam>
/// <remarks>
/// <para>Usage inside a <c>PropertyColumn</c>'s <c>FilterTemplate</c>:</para>
/// <code>
/// &lt;PropertyColumn Property="a =&gt; a.Severity" Title="Severity" Filterable="true"&gt;
///     &lt;FilterTemplate&gt;
///         &lt;EnumFilterSelect T="AnnouncementDto"
///                           TEnum="AnnouncementSeverity"
///                           FilterContext="context"
///                           DataGrid="dataGrid" /&gt;
///     &lt;/FilterTemplate&gt;
/// &lt;/PropertyColumn&gt;
/// </code>
/// </remarks>
public partial class EnumFilterSelect<T, TEnum> : ComponentBase where TEnum : struct, Enum
{
    #region Parameters

    /// <summary>
    /// The filter context passed from the <c>FilterTemplate</c> of a <see cref="MudDataGrid{T}"/> column.
    /// </summary>
    [Parameter]
    public FilterContext<T> FilterContext { get; set; } = default!;

    /// <summary>
    /// Reference to the parent <see cref="MudDataGrid{T}"/> for triggering server-side reload.
    /// </summary>
    [Parameter]
    public MudDataGrid<T> DataGrid { get; set; } = default!;

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
    /// <returns>The enum name string, or <c>null</c> if no filter is active.</returns>
    private string? GetValue()
    {
        return FilterContext.FilterDefinition?.Value as string;
    }

    /// <summary>
    /// Sets the filter value from the dropdown selection and triggers grid reload.
    /// Uses the "equals" operator for exact string matching.
    /// </summary>
    /// <param name="value">The selected enum name, or <c>null</c> to clear the filter.</param>
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

        await DataGrid.ReloadServerData();
    }

    #endregion
}
