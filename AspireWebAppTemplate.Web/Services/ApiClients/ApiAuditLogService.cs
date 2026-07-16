using System.Net.Http.Json;
using AspireWebAppTemplate.Core.Common;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Auth;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using AspireWebAppTemplate.Core.Contracts.Roles;
using AspireWebAppTemplate.Core.Contracts.Users;
using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Web.Services;

/// <summary>
/// HTTP client service for audit log querying and export.
/// Calls the API's AuditLogController endpoints.
/// </summary>
public class ApiAuditLogService
{
    #region Constructor

    /// <summary>
    /// The underlying HttpClient configured with the ApiService base address.
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of <see cref="ApiAuditLogService"/> with the configured HttpClient.
    /// </summary>
    /// <param name="http">The HttpClient instance configured via Aspire service discovery.</param>
    public ApiAuditLogService(HttpClient http)
    {
        _http = http;
    }

    #endregion

    #region Query

    /// <summary>
    /// Returns a paged list of audit log entries with optional filtering by search term, action type, entity type, and date range.
    /// </summary>
    public async Task<ApiResult<PagedResult<AuditLogEntryDto>>> GetPagedAsync(
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
        var response = await _http.GetAsync(url);
        if (response.IsSuccessStatusCode)
            return ApiResult<PagedResult<AuditLogEntryDto>>.Success(await response.Content.ReadFromJsonAsync<PagedResult<AuditLogEntryDto>>()!);
        return ApiResult<PagedResult<AuditLogEntryDto>>.Failure(await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Retrieves a single audit log entry by its unique identifier.
    /// </summary>
    public async Task<ApiResult<AuditLogEntryDto>> GetByIdAsync(Guid id)
    {
        var response = await _http.GetAsync($"/api/audit-log/{id}");
        if (response.IsSuccessStatusCode)
            return ApiResult<AuditLogEntryDto>.Success(await response.Content.ReadFromJsonAsync<AuditLogEntryDto>()!);
        return ApiResult<AuditLogEntryDto>.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Export

    /// <summary>
    /// Exports filtered audit log entries as an Excel file.
    /// </summary>
    public async Task<ApiResult<byte[]>> ExportExcelAsync(
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

        var response = await _http.GetAsync(url);
        if (response.IsSuccessStatusCode)
            return ApiResult<byte[]>.Success(await response.Content.ReadAsByteArrayAsync());
        return ApiResult<byte[]>.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion
}
