using System.Net.Http.Json;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service for audit log querying and export.
/// Calls the API's AuditLogController endpoints.
/// </summary>
public class ApiAuditLogService(HttpClient http)
{
    #region Query

    /// <summary>
    /// Returns a paged list of audit log entries with optional filtering by search term, action type, entity type, and date range.
    /// </summary>
    public async Task<PagedResult<AuditLogEntryDto>?> GetPagedAsync(
        int page = 0,
        int pageSize = 10,
        string? searchTerm = null,
        AuditActionType? actionType = null,
        AuditEntityType? entityType = null,
        DateTime? dateStart = null,
        DateTime? dateEnd = null)
    {
        var url = $"/api/audit-log?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(searchTerm))
            url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (actionType.HasValue)
            url += $"&actionType={actionType.Value}";
        if (entityType.HasValue)
            url += $"&entityType={entityType.Value}";
        if (dateStart.HasValue)
            url += $"&dateStart={dateStart.Value:O}";
        if (dateEnd.HasValue)
            url += $"&dateEnd={dateEnd.Value:O}";
        return await http.GetFromJsonAsync<PagedResult<AuditLogEntryDto>>(url);
    }

    /// <summary>
    /// Retrieves a single audit log entry by its unique identifier.
    /// </summary>
    public async Task<AuditLogEntryDto?> GetByIdAsync(Guid id)
        => await http.GetFromJsonAsync<AuditLogEntryDto>($"/api/audit-log/{id}");

    #endregion

    #region Export

    /// <summary>
    /// Exports filtered audit log entries as an Excel file.
    /// </summary>
    public async Task<byte[]?> ExportExcelAsync(
        string? searchTerm = null,
        AuditActionType? actionType = null,
        AuditEntityType? entityType = null,
        DateTime? dateStart = null,
        DateTime? dateEnd = null)
    {
        var url = "/api/audit-log/export?";
        if (!string.IsNullOrWhiteSpace(searchTerm))
            url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (actionType.HasValue)
            url += $"&actionType={actionType.Value}";
        if (entityType.HasValue)
            url += $"&entityType={entityType.Value}";
        if (dateStart.HasValue)
            url += $"&dateStart={dateStart.Value:O}";
        if (dateEnd.HasValue)
            url += $"&dateEnd={dateEnd.Value:O}";

        var response = await http.GetAsync(url);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadAsByteArrayAsync();
        return null;
    }

    #endregion
}
