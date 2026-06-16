/**
 * File download utilities for Blazor Server.
 * Used to trigger browser file downloads from byte arrays passed via JS interop.
 *
 * Usage from C#:
 *   var module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/download.js");
 *   await module.InvokeVoidAsync("downloadFile", fileName, mimeType, base64Content);
 */

/**
 * Triggers a browser file download from a Base64-encoded string.
 * Creates a temporary anchor element, sets the data URI, and clicks it.
 *
 * @param {string} fileName - The suggested file name for the download (e.g., "report.xlsx").
 * @param {string} mimeType - The MIME type of the file (e.g., "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet").
 * @param {string} base64Content - The file content encoded as a Base64 string.
 */
export function downloadFile(fileName, mimeType, base64Content) {
    const link = document.createElement("a");
    link.href = `data:${mimeType};base64,${base64Content}`;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}
