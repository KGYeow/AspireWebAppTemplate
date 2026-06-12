using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.ApiService.Data;
using AspireWebAppTemplate.Core.Contracts;
using AspireWebAppTemplate.Core.Contracts.Auth;
using AspireWebAppTemplate.Core.Contracts.AuditLog;
using AspireWebAppTemplate.Core.Contracts.Roles;
using AspireWebAppTemplate.Core.Contracts.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AspireWebAppTemplate.ApiService.Controllers;

/// <summary>
/// Provides audit log querying and Excel export capabilities.
/// </summary>
[ApiController]
[Route("api/audit-log")]
[Authorize(Roles = "Admin")]
public class AuditLogController : BaseController
{
    #region Constructor

    private readonly ApplicationDbContext _dbContext;
    private readonly IExcelExportService _excelExportService;
    private readonly IAuditLogService _auditLogService;

    public AuditLogController(
        ApplicationDbContext dbContext,
        IExcelExportService excelExportService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _excelExportService = excelExportService;
        _auditLogService = auditLogService;
    }

    #endregion

    #region Query

    /// <summary>
    /// Returns a paged list of audit log entries with optional filtering.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditLogEntryDto>>> GetAuditLog(
        [FromQuery] AuditLogQueryParams queryParams)
    {
        var query = _dbContext.AuditLogEntries.AsNoTracking().AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(queryParams.SearchTerm))
        {
            var term = queryParams.SearchTerm.ToLower();
            query = query.Where(e =>
                e.UserDisplayName.ToLower().Contains(term) ||
                e.EntityName.ToLower().Contains(term) ||
                e.Description.ToLower().Contains(term) ||
                e.EntityId.ToLower().Contains(term));
        }

        if (queryParams.ActionType.HasValue)
        {
            query = query.Where(e => e.ActionType == queryParams.ActionType.Value);
        }

        if (queryParams.EntityType.HasValue)
        {
            query = query.Where(e => e.EntityType == queryParams.EntityType.Value);
        }

        if (queryParams.DateStart.HasValue)
        {
            query = query.Where(e => e.Timestamp >= queryParams.DateStart.Value);
        }

        if (queryParams.DateEnd.HasValue)
        {
            query = query.Where(e => e.Timestamp <= queryParams.DateEnd.Value);
        }

        var totalCount = await query.CountAsync();

        var entries = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip(queryParams.Page * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .Select(e => new AuditLogEntryDto
            {
                Id = e.Id,
                UserId = e.UserId,
                UserDisplayName = e.UserDisplayName,
                ActionType = e.ActionType,
                EntityType = e.EntityType,
                EntityId = e.EntityId,
                EntityName = e.EntityName,
                Description = e.Description,
                OldValues = e.OldValues,
                NewValues = e.NewValues,
                IpAddress = e.IpAddress,
                Timestamp = e.Timestamp
            })
            .ToListAsync();

        return Ok(new PagedResult<AuditLogEntryDto>
        {
            Items = entries,
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        });
    }

    /// <summary>
    /// Returns a single audit log entry by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AuditLogEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditLogEntryDto>> GetAuditLogEntry(Guid id)
    {
        var entry = await _dbContext.AuditLogEntries
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new AuditLogEntryDto
            {
                Id = e.Id,
                UserId = e.UserId,
                UserDisplayName = e.UserDisplayName,
                ActionType = e.ActionType,
                EntityType = e.EntityType,
                EntityId = e.EntityId,
                EntityName = e.EntityName,
                Description = e.Description,
                OldValues = e.OldValues,
                NewValues = e.NewValues,
                IpAddress = e.IpAddress,
                Timestamp = e.Timestamp
            })
            .FirstOrDefaultAsync();

        if (entry is null)
            return NotFound();

        return Ok(entry);
    }

    #endregion

    #region Export

    /// <summary>
    /// Exports audit log entries matching the query to an Excel file.
    /// Capped at 50,000 rows.
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportAuditLog([FromQuery] AuditLogQueryParams queryParams)
    {
        const int maxExportRows = 50_000;

        var query = _dbContext.AuditLogEntries.AsNoTracking().AsQueryable();

        // Apply same filters as GetAuditLog
        if (!string.IsNullOrWhiteSpace(queryParams.SearchTerm))
        {
            var term = queryParams.SearchTerm.ToLower();
            query = query.Where(e =>
                e.UserDisplayName.ToLower().Contains(term) ||
                e.EntityName.ToLower().Contains(term) ||
                e.Description.ToLower().Contains(term) ||
                e.EntityId.ToLower().Contains(term));
        }

        if (queryParams.ActionType.HasValue)
        {
            query = query.Where(e => e.ActionType == queryParams.ActionType.Value);
        }

        if (queryParams.EntityType.HasValue)
        {
            query = query.Where(e => e.EntityType == queryParams.EntityType.Value);
        }

        if (queryParams.DateStart.HasValue)
        {
            query = query.Where(e => e.Timestamp >= queryParams.DateStart.Value);
        }

        if (queryParams.DateEnd.HasValue)
        {
            query = query.Where(e => e.Timestamp <= queryParams.DateEnd.Value);
        }

        var entries = await query
            .OrderByDescending(e => e.Timestamp)
            .Take(maxExportRows)
            .Select(e => new AuditLogEntryDto
            {
                Id = e.Id,
                UserId = e.UserId,
                UserDisplayName = e.UserDisplayName,
                ActionType = e.ActionType,
                EntityType = e.EntityType,
                EntityId = e.EntityId,
                EntityName = e.EntityName,
                Description = e.Description,
                OldValues = e.OldValues,
                NewValues = e.NewValues,
                IpAddress = e.IpAddress,
                Timestamp = e.Timestamp
            })
            .ToListAsync();

        var fileBytes = _excelExportService.ExportToExcel(entries, "Audit Log");
        var fileName = $"AuditLog_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    #endregion
}
