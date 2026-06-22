using System.Text.Json;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using AspireWebAppTemplate.Web.Abstractions;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.AuditLog;

/// <summary>
/// Dialog component that displays the full details of a single audit log entry.
/// Shows all entry fields in a key-value table with pretty-printed JSON for OldValues/NewValues.
/// </summary>
public partial class AuditLogDetailDialog : ComponentBase
{
    #region Injected Services

    [Inject] private IUserTimeZoneContext TimeZoneContext { get; set; } = default!;

    #endregion

    #region Cascading Parameters

    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    #endregion

    #region Parameters

    /// <summary>
    /// The audit log entry DTO to display in the detail view.
    /// </summary>
    [Parameter] public AuditLogEntryDto Entry { get; set; } = default!;

    #endregion

    #region Private Methods

    private static string FormatJson(string? json)
    {
        if (json is null)
            return "N/A";

        try
        {
            var jsonDocument = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(jsonDocument, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return json;
        }
    }

    #endregion
}
