using AspireWebAppTemplate.Application.Common;
using AspireWebAppTemplate.Application.Features.Template.Announcements;

namespace AspireWebAppTemplate.Application.Features.Template.Announcements;

/// <summary>
/// Defines the contract for announcement business logic including creation, retrieval,
/// status filtering, and per-user dismissal management. All database access for announcements
/// is encapsulated here — controllers delegate to this service without touching DbContext.
/// </summary>
/// <remarks>
/// <para>
/// Implementations sanitize HTML content (via Ganss.Xss.HtmlSanitizer) on both create and update
/// code paths before persistence, ensuring XSS protection regardless of input source.
/// </para>
/// <para>
/// Implementations should be registered as scoped services to align with the per-request
/// <c>DbContext</c> lifetime.
/// </para>
/// </remarks>
public interface IAnnouncementService
{
    #region CRUD Operations

    /// <summary>
    /// Creates a new announcement with the specified fields, sanitizes HTML content,
    /// sets audit metadata (CreatedByUserId, CreatedAtUtc, UpdatedAtUtc), and optionally
    /// sends notifications to all active users when the announcement is immediately active.
    /// </summary>
    /// <param name="request">
    /// A <see cref="CreateAnnouncementRequest"/> containing the title, message (HTML content),
    /// display type, severity, scheduling dates, activation flag, and notification preference.
    /// </param>
    /// <returns>
    /// A task that resolves to an <see cref="AnnouncementDto"/> representing the newly created
    /// announcement with all fields populated including the computed status.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the title exceeds 200 characters, the message exceeds 10000 characters,
    /// or StartsAtUtc is on or after ExpiresAtUtc (when both are provided).
    /// </exception>
    Task<AnnouncementDto> CreateAsync(CreateAnnouncementRequest request);

    /// <summary>
    /// Updates an existing announcement with the specified fields, sanitizes HTML content,
    /// refreshes UpdatedAtUtc, optionally clears all dismissal records, and optionally
    /// sends notifications when the announcement is currently active.
    /// </summary>
    /// <param name="id">The unique identifier of the announcement to update.</param>
    /// <param name="request">
    /// An <see cref="UpdateAnnouncementRequest"/> containing the updated fields,
    /// ClearDismissals flag, and NotifyUsers flag for this specific edit.
    /// </param>
    /// <returns>
    /// A task that resolves to an <see cref="AnnouncementDto"/> representing the updated
    /// announcement with all fields populated including the recomputed status.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no announcement exists with the specified <paramref name="id"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the title exceeds 200 characters, the message exceeds 10000 characters,
    /// or StartsAtUtc is on or after ExpiresAtUtc (when both are provided).
    /// </exception>
    Task<AnnouncementDto> UpdateAsync(Guid id, UpdateAnnouncementRequest request);

    /// <summary>
    /// Deletes an announcement and all associated dismissal records.
    /// </summary>
    /// <param name="id">The unique identifier of the announcement to delete.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no announcement exists with the specified <paramref name="id"/>.
    /// </exception>
    Task DeleteAsync(Guid id);

    #endregion

    #region Query Operations

    /// <summary>
    /// Retrieves all announcements ordered by CreatedAtUtc descending.
    /// Used by the admin management page for full CRUD listing.
    /// </summary>
    /// <returns>
    /// A task that resolves to a list of <see cref="AnnouncementDto"/> objects representing
    /// all announcements ordered by creation date descending.
    /// </returns>
    Task<List<AnnouncementDto>> GetAllAsync();

    /// <summary>
    /// Retrieves active, non-dismissed announcements for the specified user.
    /// Returns both Banner-type and Standard-type announcements that satisfy the active criteria
    /// and have not been dismissed by the user. Results are ordered by priority (Severity descending,
    /// then CreatedAtUtc descending).
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose active announcements are being queried.</param>
    /// <returns>
    /// A task that resolves to a list of <see cref="AnnouncementDto"/> objects representing
    /// active, non-dismissed announcements for the user, ordered by priority.
    /// </returns>
    Task<List<AnnouncementDto>> GetActiveForUserAsync(string userId);

    /// <summary>
    /// Retrieves active announcements plus announcements that expired within the last 30 days,
    /// with server-side pagination and optional severity filtering.
    /// Used by the announcement list page for displaying current and recently expired announcements.
    /// Results are ordered by CreatedAtUtc descending.
    /// </summary>
    /// <param name="queryParams">Pagination and filter parameters.</param>
    /// <returns>
    /// A task that resolves to a <see cref="PagedResult{T}"/> containing the matching announcements
    /// and total count for pagination.
    /// </returns>
    Task<PagedResult<AnnouncementDto>> GetForListPageAsync(AnnouncementQueryParams queryParams);

    #endregion

    #region Dismissal

    /// <summary>
    /// Records a per-user dismissal of the specified announcement. Idempotent — if the user
    /// has already dismissed this announcement, the operation completes successfully without
    /// creating a duplicate record.
    /// </summary>
    /// <param name="userId">The unique identifier of the user dismissing the announcement.</param>
    /// <param name="announcementId">The unique identifier of the announcement being dismissed.</param>
    /// <returns>A task representing the asynchronous dismissal operation.</returns>
    Task DismissAsync(string userId, Guid announcementId);

    #endregion
}
