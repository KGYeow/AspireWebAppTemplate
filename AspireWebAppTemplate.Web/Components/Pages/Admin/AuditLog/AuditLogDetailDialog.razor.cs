using System.Text.Json;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Auth;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using AspireWebAppTemplate.Core.Contracts.Roles;
using AspireWebAppTemplate.Core.Contracts.Users;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using AspireWebAppTemplate.Web.Abstractions;

namespace AspireWebAppTemplate.Web.Components.Pages.Admin.AuditLog;

/// <summary>
/// Dialog component that displays the full details of a single audit log entry.
/// Shows all entry fields including pretty-printed JSON for OldValues/NewValues.
/// </summary>
public partial class AuditLogDetailDialog : ComponentBase
{
    #region Cascading Parameters

    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    #endregion

    #region Parameters

    /// <summary>
    /// The audit log entry DTO to display in the detail view.
    /// </summary>
    [Parameter] public AuditLogEntryDto Entry { get; set; } = default!;

    #endregion

    #region Injected Services

    [Inject] private IUserTimeZoneContext TimeZoneContext { get; set; } = default!;

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

    private void Close() => MudDialog.Cancel();

    #endregion
}
