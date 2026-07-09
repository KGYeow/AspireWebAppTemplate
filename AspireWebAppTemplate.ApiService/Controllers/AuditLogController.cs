using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspireWebAppTemplate.ApiService.Controllers;

/// <summary>
/// Provides audit log querying and Excel export capabilities.
/// Delegates all data retrieval to <see cref="IAuditLogService"/> and
/// export formatting to <see cref="IExcelExportService"/>.
/// </summary>
[Route("api/audit-log")]
[Authorize]
public class AuditLogController : BaseController
{
    #region Constructor

    private readonly IAuditLogService _auditLogService;
    private readonly IExcelExportService _excelExportService;

    /// <summary>
    /// Initializes a new instance of <see cref="AuditLogController"/>.
    /// </summary>
    /// <param name="auditLogService">Service for querying and filtering audit log entries.</param>
    /// <param name="excelExportService">Service for generating Excel export files.</param>
    public AuditLogController(IAuditLogService auditLogService, IExcelExportService excelExportService)
    {
        _auditLogService = auditLogService;
        _excelExportService = excelExportService;
    }

    #endregion

    #region Query

    /// <summary>
    /// Returns a paged list of audit log entries with optional filtering.
    /// </summary>
    /// <param name="queryParams">Pagination and filter criteria.</param>
    /// <returns>A paged result containing matching audit log entries.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditLogEntryDto>>> GetAuditLog([FromQuery] AuditLogQueryParams queryParams)
    {
        var result = await _auditLogService.SearchAsync(queryParams);
        return Ok(result);
    }

    /// <summary>
    /// Returns a single audit log entry by ID.
    /// </summary>
    /// <param name="id">The unique identifier of the audit log entry.</param>
    /// <returns>The audit log entry matching the specified ID.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AuditLogEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditLogEntryDto>> GetAuditLogEntry(Guid id)
    {
        try
        {
            var entry = await _auditLogService.GetByIdAsync(id);
            return Ok(entry);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    #endregion

    #region Export

    /// <summary>
    /// Exports audit log entries matching the query to an Excel file.
    /// Capped at 50,000 rows.
    /// </summary>
    /// <param name="queryParams">Filter criteria for the export.</param>
    /// <returns>An Excel file containing the matching audit log entries.</returns>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportAuditLog([FromQuery] AuditLogQueryParams queryParams)
    {
        var entries = await _auditLogService.GetForExportAsync(queryParams);
        var fileBytes = _excelExportService.ExportToExcel(entries, "Audit Log");
        var fileName = $"AuditLog_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    #endregion
}
