using System.Text.Json;
using BlazorWebAppTemplate.Abstractions;
using BlazorWebAppTemplate.Data.Entities;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace BlazorWebAppTemplate.Components.Pages.AuditLog;

/// <summary>
/// Dialog component that displays the full details of a single <see cref="AuditLogEntry"/>.
/// Shows all entry fields including pretty-printed JSON for OldValues/NewValues,
/// and displays "N/A" for null optional fields (OldValues, NewValues, IpAddress).
/// </summary>
/// <remarks>
/// Opened from the audit log data grid when an administrator clicks a row.
/// Uses <see cref="IUserTimeZoneContext"/> to format the timestamp in the user's configured timezone.
/// The dialog is dismissed via the Close button, which returns focus to the grid.
/// </remarks>
public partial class AuditLogDetailDialog : ComponentBase
{
    #region Cascading Parameters

    /// <summary>
    /// The MudBlazor dialog instance used to control the lifecycle of this dialog.
    /// </summary>
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    #endregion

    #region Parameters

    /// <summary>
    /// The audit log entry to display in the detail view.
    /// Passed as a dialog parameter from the parent page.
    /// </summary>
    [Parameter] public AuditLogEntry Entry { get; set; } = default!;

    #endregion

    #region Injected Services

    /// <summary>
    /// Provides user-aware datetime formatting using the current user's configured time zone.
    /// </summary>
    [Inject] private IUserTimeZoneContext TimeZoneContext { get; set; } = default!;

    #endregion

    #region Private Methods

    /// <summary>
    /// Formats a JSON string with indentation for display, or returns "N/A" if the value is null.
    /// Attempts to parse and re-serialize the JSON with indentation; if parsing fails,
    /// returns the raw string as-is.
    /// </summary>
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

    /// <summary>
    /// Closes the dialog and returns focus to the data grid.
    /// </summary>
    private void Close() => MudDialog.Cancel();

    #endregion
}
