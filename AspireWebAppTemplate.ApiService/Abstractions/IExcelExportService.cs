namespace AspireWebAppTemplate.Abstractions;

/// <summary>
/// Defines the contract for generating Excel (.xlsx) and CSV exports from data collections.
/// Supports both generic exports (any <see cref="IEnumerable{T}"/> to Excel) and
/// domain-specific exports that may involve database access.
/// </summary>
/// <remarks>
/// Register as scoped to align with DbContext lifetime in Blazor Server circuits.
/// Uses EPPlus for Excel generation. Properties must be decorated with
/// <c>[Exportable]</c> from the Core project to be included in exports.
/// </remarks>
public interface IExcelExportService
{
    /// <summary>
    /// Exports a collection of data to an Excel (.xlsx) file as a byte array.
    /// Only properties decorated with <c>[Exportable]</c> are included as columns.
    /// Column headers are derived from <c>[Display(Name = "...")]</c> or PascalCase splitting.
    /// </summary>
    /// <typeparam name="T">The type of items to export.</typeparam>
    /// <param name="data">The collection of items to export as rows.</param>
    /// <param name="sheetName">The worksheet name (defaults to "Sheet1" if null).</param>
    /// <returns>A byte array containing the Excel file content.</returns>
    byte[] ExportToExcel<T>(IEnumerable<T> data, string? sheetName = null) where T : class;

    /// <summary>
    /// Exports a collection of data to a CSV file as a byte array (UTF-8 encoded with BOM).
    /// Only properties decorated with <c>[Exportable]</c> are included as columns.
    /// </summary>
    /// <typeparam name="T">The type of items to export.</typeparam>
    /// <param name="data">The collection of items to export as rows.</param>
    /// <returns>A byte array containing the CSV file content (UTF-8 with BOM).</returns>
    byte[] ExportToCsv<T>(IEnumerable<T> data) where T : class;

    /// <summary>
    /// Exports multiple data sets to a single Excel file with multiple worksheets.
    /// Each entry in the dictionary represents a sheet name and its corresponding data.
    /// </summary>
    /// <param name="sheets">A dictionary mapping sheet names to data collections.</param>
    /// <returns>A byte array containing the Excel file content.</returns>
    byte[] ExportToExcelMultiSheet(Dictionary<string, IEnumerable<object>> sheets);
}
