using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.Infrastructure.Services.Template.Announcements;
using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Infrastructure.Services.Template.Announcements;
using AspireWebAppTemplate.Application.Features.Template.Announcements;
using AspireWebAppTemplate.Infrastructure.Services.Template.Announcements;
using AspireWebAppTemplate.Domain.Enums;
using AspireWebAppTemplate.Infrastructure.Services.Template.Announcements;
using Microsoft.AspNetCore.Authorization;
using AspireWebAppTemplate.Infrastructure.Services.Template.Announcements;
using Microsoft.AspNetCore.Mvc;
using AspireWebAppTemplate.Infrastructure.Services.Template.Announcements;

namespace AspireWebAppTemplate.ApiService.Controllers.Template;

/// <summary>
/// Provides announcement query, mutation, and dismissal endpoints.
/// This controller is intentionally thin — it handles HTTP concerns only (request parsing,
/// user identity extraction, status code mapping) and delegates all business logic to
/// <see cref="IAnnouncementService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Endpoints are grouped into three categories:
/// <list type="bullet">
///   <item>Query endpoints — accessible to all authenticated users for retrieving active/list announcements.</item>
///   <item>Admin endpoints — accessible to administrators for full CRUD management.</item>
///   <item>Dismissal — accessible to all authenticated users for per-user banner dismissal.</item>
/// </list>
/// </para>
/// <para>
/// Exception-to-HTTP-status mapping:
/// <list type="bullet">
///   <item><see cref="KeyNotFoundException"/> → 404 Not Found</item>
///   <item><see cref="InvalidOperationException"/> → 400 Bad Request</item>
///   <item><see cref="ArgumentException"/> → 400 Bad Request</item>
/// </list>
/// </para>
/// </remarks>
[Route("api/announcements")]
[Authorize]
public class AnnouncementController : BaseController
{
    #region Constructor

    /// <summary>
    /// The announcement service for managing all announcement operations.
    /// </summary>
    private readonly IAnnouncementService _announcementService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnnouncementController"/> class.
    /// </summary>
    /// <param name="announcementService">The announcement service for managing all announcement operations.</param>
    public AnnouncementController(IAnnouncementService announcementService)
    {
        _announcementService = announcementService;
    }

    #endregion

    #region Query Endpoints

    /// <summary>
    /// Retrieves active, non-dismissed announcements for the authenticated user.
    /// Results are ordered by priority (Severity descending, then CreatedAtUtc descending).
    /// Used by the banner and dashboard components via AnnouncementContext.
    /// </summary>
    /// <returns>A list of active, non-dismissed announcements for the current user.</returns>
    /// <response code="200">Returns the list of active announcements for the user.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("active")]
    [ProducesResponseType(typeof(List<AnnouncementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<AnnouncementDto>>> GetActiveForUser()
    {
        var result = await _announcementService.GetActiveForUserAsync(CurrentUserId!);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves active announcements plus announcements that expired within the last 30 days,
    /// with server-side pagination and optional severity filtering.
    /// Used by the announcement list page for progressive loading.
    /// Results are ordered by CreatedAtUtc descending.
    /// </summary>
    /// <param name="queryParams">Pagination and filter parameters.</param>
    /// <returns>A paginated result of announcements.</returns>
    /// <response code="200">Returns the paginated announcements for the list page.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("list")]
    [ProducesResponseType(typeof(PagedResult<AnnouncementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<AnnouncementDto>>> GetForListPage([FromQuery] AnnouncementQueryParams queryParams)
    {
        var result = await _announcementService.GetForListPageAsync(queryParams);
        return Ok(result);
    }

    #endregion

    #region Admin Endpoints

    /// <summary>
    /// Retrieves all announcements ordered by CreatedAtUtc descending.
    /// Used by the admin management page for full CRUD listing.
    /// Client-side filtering is handled by the DataGrid.
    /// </summary>
    /// <returns>A list of all announcements.</returns>
    /// <response code="200">Returns all announcements.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<AnnouncementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<AnnouncementDto>>> GetAll()
    {
        var result = await _announcementService.GetAllAsync();
        return Ok(result);
    }

    /// <summary>
    /// Creates a new announcement with the specified fields. Sanitizes HTML content,
    /// sets audit metadata, and optionally sends notifications to all active users.
    /// </summary>
    /// <param name="request">The announcement creation request containing all required fields.</param>
    /// <returns>The newly created announcement DTO with computed status.</returns>
    /// <response code="200">The announcement was created successfully.</response>
    /// <response code="400">Validation failed (title/content length, invalid date range).</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AnnouncementDto>> Create([FromBody] CreateAnnouncementRequest request)
    {
        try
        {
            var result = await _announcementService.CreateAsync(request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Updates an existing announcement with the specified fields. Sanitizes HTML content,
    /// refreshes UpdatedAtUtc, optionally clears dismissals, and optionally sends notifications.
    /// </summary>
    /// <param name="id">The unique identifier of the announcement to update.</param>
    /// <param name="request">The announcement update request containing updated fields and flags.</param>
    /// <returns>The updated announcement DTO with recomputed status.</returns>
    /// <response code="200">The announcement was updated successfully.</response>
    /// <response code="400">Validation failed (title/content length, invalid date range).</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">No announcement exists with the specified ID.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementDto>> Update(Guid id, [FromBody] UpdateAnnouncementRequest request)
    {
        try
        {
            var result = await _announcementService.UpdateAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>
    /// Deletes an announcement and all associated dismissal records.
    /// </summary>
    /// <param name="id">The unique identifier of the announcement to delete.</param>
    /// <returns>200 OK on success.</returns>
    /// <response code="200">The announcement was deleted successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">No announcement exists with the specified ID.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _announcementService.DeleteAsync(id);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    #endregion

    #region Dismissal

    /// <summary>
    /// Dismisses an announcement for the authenticated user. Creates a per-user dismissal record
    /// so the announcement no longer appears in the user's banner. Idempotent — succeeds even if
    /// the announcement is already dismissed by this user.
    /// </summary>
    /// <param name="id">The unique identifier of the announcement to dismiss.</param>
    /// <returns>200 OK on success.</returns>
    /// <response code="200">The announcement was dismissed successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("{id:guid}/dismiss")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Dismiss(Guid id)
    {
        await _announcementService.DismissAsync(CurrentUserId!, id);
        return Ok();
    }

    #endregion
}
