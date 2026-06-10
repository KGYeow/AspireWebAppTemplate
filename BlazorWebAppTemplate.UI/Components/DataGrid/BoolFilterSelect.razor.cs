using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace BlazorWebAppTemplate.UI.Components.DataGrid;

/// <summary>
/// Reusable dropdown filter component for boolean columns in <see cref="MudDataGrid{T}"/>.
/// Renders a <see cref="MudSelect{T}"/> with two options mapped to <c>true</c> and <c>false</c>.
/// </summary>
/// <typeparam name="T">The data item type of the parent <see cref="MudDataGrid{T}"/>.</typeparam>
/// <remarks>
/// <para>Usage inside a <c>PropertyColumn</c>'s <c>FilterTemplate</c>:</para>
/// <code>
/// &lt;PropertyColumn Property="u =&gt; u.IsActive" Title="Status" Filterable="true"&gt;
///     &lt;FilterTemplate&gt;
///         &lt;BoolFilterSelect T="UserViewModel"
///                           FilterContext="context"
///                           DataGrid="dataGrid"
///                           TrueLabel="Active"
///                           FalseLabel="Inactive" /&gt;
///     &lt;/FilterTemplate&gt;
/// &lt;/PropertyColumn&gt;
/// </code>
/// </remarks>
public partial class BoolFilterSelect<T> : ComponentBase
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
    /// Display text for the <c>true</c> value. Defaults to "Yes".
    /// </summary>
    [Parameter]
    public string TrueLabel { get; set; } = "Yes";

    /// <summary>
    /// Display text for the <c>false</c> value. Defaults to "No".
    /// </summary>
    [Parameter]
    public string FalseLabel { get; set; } = "No";

    /// <summary>
    /// Placeholder text shown when no filter is selected. Defaults to "All".
    /// </summary>
    [Parameter]
    public string Placeholder { get; set; } = "All";

    #endregion

    #region Methods

    /// <summary>
    /// Gets the current display value from the filter definition.
    /// Maps the underlying bool to the corresponding label.
    /// </summary>
    /// <returns>The label string, or <c>null</c> if no filter is active.</returns>
    private string? GetValue()
    {
        if (FilterContext.FilterDefinition?.Value is bool b)
            return b ? TrueLabel : FalseLabel;
        return null;
    }

    /// <summary>
    /// Sets the filter value from the dropdown selection.
    /// Maps the label back to a bool and triggers grid reload.
    /// </summary>
    /// <param name="value">The selected label, or <c>null</c> to clear the filter.</param>
    private async Task SetValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            FilterContext.FilterDefinition!.Value = null;
            FilterContext.FilterDefinition.Operator = null;
        }
        else
        {
            FilterContext.FilterDefinition!.Value = value == TrueLabel;
            FilterContext.FilterDefinition.Operator = FilterOperator.Boolean.Is;
        }

        await DataGrid.ReloadServerData();
    }

    #endregion
}
