using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.AuditLog;
using AspireWebAppTemplate.Web.Extensions;
using AspireWebAppTemplate.Web.Utilities;

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
        // page and pageSize are always emitted; filters and sort options only when present.
        // Dates use the round-trip ("O") format to match the exact wire form the API expects.
        var url = QueryStringBuilder.Build("/api/audit-log", qs =>
        {
            qs.Add("page", queryParams.Page);
            qs.Add("pageSize", queryParams.PageSize);
            qs.AddIfNotWhiteSpace("searchTerm", queryParams.SearchTerm);
            qs.AddIfHasValue("actionType", queryParams.ActionType);
            qs.AddIfHasValue("entityType", queryParams.EntityType);
            if (queryParams.DateStart.HasValue)
                qs.Add("dateStart", queryParams.DateStart.Value.ToString("O"));
            if (queryParams.DateEnd.HasValue)
                qs.Add("dateEnd", queryParams.DateEnd.Value.ToString("O"));
            qs.AddIfNotWhiteSpace("sortBy", queryParams.SortBy);
            if (!queryParams.SortDescending)
                qs.Add("sortDescending", "false");
        });

        var response = await _http.GetAsync(url);
        return await response.ToApiResultAsync<PagedResult<AuditLogEntryDto>>();
    }

    /// <summary>
    /// Retrieves a single audit log entry by its unique identifier.
    /// </summary>
    public async Task<ApiResult<AuditLogEntryDto>> GetByIdAsync(Guid id)
    {
        var response = await _http.GetAsync($"/api/audit-log/{id}");
        return await response.ToApiResultAsync<AuditLogEntryDto>();
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
        // Only the filter parameters are emitted; the builder omits the '?' when no filters apply,
        // removing the "?&" / bare-trailing-"?" quirk of the previous hand-built string.
        // Dates use the round-trip ("O") format to match the exact wire form the API expects.
        var url = QueryStringBuilder.Build("/api/audit-log/export", qs =>
        {
            qs.AddIfNotWhiteSpace("searchTerm", queryParams.SearchTerm);
            qs.AddIfHasValue("actionType", queryParams.ActionType);
            qs.AddIfHasValue("entityType", queryParams.EntityType);
            if (queryParams.DateStart.HasValue)
                qs.Add("dateStart", queryParams.DateStart.Value.ToString("O"));
            if (queryParams.DateEnd.HasValue)
                qs.Add("dateEnd", queryParams.DateEnd.Value.ToString("O"));
        });

        var response = await _http.GetAsync(url);
        if (response.IsSuccessStatusCode)
            return ApiResult<byte[]>.Success(await response.Content.ReadAsByteArrayAsync());
        return ApiResult<byte[]>.Failure(await response.Content.ReadAsStringAsync());
    }

    #endregion
}
