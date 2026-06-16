/**
 * Browser timezone detection utility for Blazor Server.
 * Used to auto-detect the user's IANA timezone on first login.
 *
 * Usage from C#:
 *   var module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/timezone.js");
 *   var timeZone = await module.InvokeAsync<string?>("getBrowserTimeZone");
 */

/**
 * Detects the browser's IANA timezone identifier (e.g., "Asia/Kuala_Lumpur", "America/New_York").
 * Uses the Intl API which is supported in all modern browsers.
 *
 * @returns {string|null} The IANA timezone ID, or null if detection fails.
 */
export function getBrowserTimeZone() {
    try {
        return Intl.DateTimeFormat().resolvedOptions().timeZone;
    } catch {
        return null;
    }
}
