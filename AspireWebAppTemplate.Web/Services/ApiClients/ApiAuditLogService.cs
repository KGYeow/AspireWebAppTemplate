using System.Net.Http.Json;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Contracts.AuditLog;

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
    /// Returns a paged list of audit log entries using the specified query parameters
    /// for filtering, sorting, and pagination.
    /// </summary>
    /// <param name="queryParams">The query parameters containing page, pageSize, filters, and sort options.</param>
    /// <returns>An <see cref="ApiResult{T}"/> containing the paged audit log entries on success.</returns>
    public async Task<ApiResult<PagedResult<AuditLogEntryDto>>> GetPagedAsync(AuditLogQueryParams queryParams)
    {
        var url = $"/api/audit-log?page={queryParams.Page}&pageSize={queryParams.PageSize}";
        if (!string.IsNullOrWhiteSpace(queryParams.SearchTerm))
            url += $"&searchTerm={Uri.EscapeDataString(queryParams.SearchTerm)}";
        if (queryParams.ActionType.HasValue)
            url += $"&actionType={queryParams.ActionType.Value}";
        if (queryParams.EntityType.HasValue)
            url += $"&entityType={queryParams.EntityType.Value}";
        if (queryParams.DateStart.HasValue)
            url += $"&dateStart={queryParams.DateStart.Value:O}";
        if (queryParams.DateEnd.HasValue)
            url += $"&dateEnd={queryParams.DateEnd.Value:O}";
        if (!string.IsNullOrWhiteSpace(queryParams.SortBy))
            url += $"&sortBy={Uri.EscapeDataString(queryParams.SortBy)}";
        if (!queryParams.SortDescending)
            url += "&sortDescending=false";

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
    /// Exports filtered audit log entries as an Excel file using the specified query parameters.
    /// </summary>
    /// <param name="queryParams">The query parameters containing filters for the export.</param>
    /// <returns>An <see cref="ApiResult{T}"/> containing the Excel file bytes on success.</returns>
    public async Task<ApiResult<byte[]>> ExportExcelAsync(AuditLogQueryParams queryParams)
    {
        var url = "/api/audit-log/export?";
        if (!string.IsNullOrWhiteSpace(queryParams.SearchTerm))
            url += $"&searchTerm={Uri.EscapeDataString(queryParams.SearchTerm)}";
        if (queryParams.ActionType.HasValue)
            url += $"&actionType={queryParams.ActionType.Value}";
        if (queryParams.EntityType.HasValue)
            url += $"&entityType={queryParams.EntityType.Value}";
        if (queryParams.DateStart.HasValue)
            url += $"&dateStart={queryParams.DateStart.Value:O}";
        if (queryParams.DateEnd.HasValue)
            url += $"&dateEnd={queryParams.DateEnd.Value:O}";

        var response = await _http.GetAsync(url);
        if (response.IsSuccessStatusCode)
            return ApiResult<byte[]>.Success(await response.Content.ReadAsByteArrayAsync());
        return ApiResult<byte[]>.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion
}
