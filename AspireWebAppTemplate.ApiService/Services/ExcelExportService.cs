using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using AspireWebAppTemplate.Abstractions;
using AspireWebAppTemplate.Core.Domain.Enums;
using AspireWebAppTemplate.Core.Utilities;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace AspireWebAppTemplate.Services;

/// <summary>
/// Generates Excel (.xlsx) and CSV exports from data collections using EPPlus.
/// Supports generic exports with automatic property discovery using
/// <see cref="ExportColumnAttribute"/> from the Core project for column configuration.
/// </summary>
/// <remarks>
/// Registered as a scoped service. Properties marked with <see cref="ExportColumnAttribute"/>
/// are included in export; properties without it are excluded.
/// Column headers are derived from <see cref="DisplayAttribute.Name"/> or PascalCase splitting.
/// </remarks>
public class ExcelExportService : IExcelExportService
{
    /// <inheritdoc />
    public byte[] ExportToExcel<T>(IEnumerable<T> data, string? sheetName = null) where T : class
    {
        var columns = GetExportColumns<T>();

        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add(sheetName ?? "Sheet1");

        WriteHeader(ws, columns);
        WriteRows(ws, columns, data);
        AutoFitColumns(ws, columns);

        return package.GetAsByteArray();
    }

    /// <inheritdoc />
    public byte[] ExportToCsv<T>(IEnumerable<T> data) where T : class
    {
        var columns = GetExportColumns<T>();
        var sb = new StringBuilder();

        // Header row
        sb.AppendLine(string.Join(",", columns.Select(c => EscapeCsvField(c.Header))));

        // Data rows
        foreach (var item in data)
        {
            var values = columns.Select(c =>
            {
                var value = c.Property.GetValue(item);
                var formatted = FormatValue(value, c.Format);
                // Apply NullText fallback when value is null/empty
                if (string.IsNullOrEmpty(formatted) && c.NullText is not null)
                    formatted = c.NullText;
                return EscapeCsvField(formatted);
            });
            sb.AppendLine(string.Join(",", values));
        }

        // UTF-8 with BOM for Excel compatibility
        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + content.Length];
        preamble.CopyTo(result, 0);
        content.CopyTo(result, preamble.Length);

        return result;
    }

    /// <inheritdoc />
    public byte[] ExportToExcelMultiSheet(Dictionary<string, IEnumerable<object>> sheets)
    {
        using var package = new ExcelPackage();

        foreach (var (name, data) in sheets)
        {
            var dataList = data.ToList();
            if (dataList.Count == 0) continue;

            var itemType = dataList[0].GetType();
            var columns = GetExportColumns(itemType);

            var ws = package.Workbook.Worksheets.Add(name);
            WriteHeader(ws, columns);
            WriteRowsUntyped(ws, columns, dataList);
            AutoFitColumns(ws, columns);
        }

        return package.GetAsByteArray();
    }

    #region Column Discovery

    /// <summary>
    /// Discovers exportable columns from the type's properties decorated with <see cref="ExportColumnAttribute"/>.
    /// Column header is resolved from <see cref="DisplayAttribute.Name"/> or PascalCase split of property name.
    /// Format is resolved from <see cref="DisplayFormatAttribute.DataFormatString"/>.
    /// </summary>
    private static List<ExportColumn> GetExportColumns<T>() where T : class
    {
        return GetExportColumns(typeof(T));
    }

    /// <summary>
    /// Discovers exportable columns from a runtime type's properties.
    /// Only properties with <see cref="ExportColumnAttribute"/> are included.
    /// </summary>
    private static List<ExportColumn> GetExportColumns(Type type, ExportScope scope = ExportScope.All)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(prop =>
            {
                var exportAttr = prop.GetCustomAttribute<ExportColumnAttribute>();

                // Only include properties marked with [ExportColumn]
                if (exportAttr is null)
                    return null;

                // Check scope: include if scope matches or property is All
                if (exportAttr.Scope != ExportScope.All && exportAttr.Scope != scope)
                    return null;

                // Resolve header name: [Display(Name = "...")] > PascalCase split
                var displayAttr = prop.GetCustomAttribute<DisplayAttribute>();
                var header = displayAttr?.Name ?? SplitPascalCase(prop.Name);

                // Resolve format: [DisplayFormat(DataFormatString = "...")]
                var formatAttr = prop.GetCustomAttribute<DisplayFormatAttribute>();
                var format = formatAttr?.DataFormatString?.Replace("{0:", "").Replace("}", "");

                return new ExportColumn
                {
                    Property = prop,
                    Header = header,
                    Order = exportAttr.Order,
                    Format = format,
                    NullText = exportAttr.NullText
                };
            })
            .Where(c => c is not null)
            .OrderBy(c => c!.Order)
            .Cast<ExportColumn>()
            .ToList();
    }

    #endregion

    #region Excel Writing

    /// <summary>
    /// Writes the header row with bold styling and a bottom border.
    /// </summary>
    private static void WriteHeader(ExcelWorksheet ws, List<ExportColumn> columns)
    {
        for (var col = 0; col < columns.Count; col++)
        {
            var cell = ws.Cells[1, col + 1];
            cell.Value = columns[col].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        }
    }

    /// <summary>
    /// Writes data rows with optional number/date formatting.
    /// </summary>
    private static void WriteRows<T>(ExcelWorksheet ws, List<ExportColumn> columns, IEnumerable<T> data)
    {
        var row = 2;
        foreach (var item in data)
        {
            for (var col = 0; col < columns.Count; col++)
            {
                var value = columns[col].Property.GetValue(item);
                var cell = ws.Cells[row, col + 1];
                SetCellValue(cell, value, columns[col]);
            }
            row++;
        }
    }

    /// <summary>
    /// Writes data rows from an untyped collection (used for multi-sheet export).
    /// </summary>
    private static void WriteRowsUntyped(ExcelWorksheet ws, List<ExportColumn> columns, List<object> data)
    {
        var row = 2;
        foreach (var item in data)
        {
            for (var col = 0; col < columns.Count; col++)
            {
                var value = columns[col].Property.GetValue(item);
                var cell = ws.Cells[row, col + 1];
                SetCellValue(cell, value, columns[col]);
            }
            row++;
        }
    }

    /// <summary>
    /// Sets a cell value with appropriate type handling, NullText fallback, and optional format.
    /// </summary>
    private static void SetCellValue(ExcelRange cell, object? value, ExportColumn column)
    {
        // Apply NullText fallback when value is null
        if (value is null)
        {
            cell.Value = column.NullText ?? string.Empty;
            return;
        }

        cell.Value = value switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.DateTime,
            bool b => b ? "Yes" : "No",
            Enum e => e.ToString(),
            _ => value
        };

        // Apply format if specified
        if (!string.IsNullOrEmpty(column.Format))
        {
            cell.Style.Numberformat.Format = column.Format;
        }
        else if (value is DateTime or DateTimeOffset)
        {
            cell.Style.Numberformat.Format = "yyyy-MM-dd HH:mm:ss";
        }
    }

    /// <summary>
    /// Auto-fits column widths with a reasonable max to prevent excessively wide columns.
    /// </summary>
    private static void AutoFitColumns(ExcelWorksheet ws, List<ExportColumn> columns)
    {
        for (var col = 1; col <= columns.Count; col++)
        {
            ws.Column(col).AutoFit(10, 50);
        }
    }

    #endregion

    #region CSV Helpers

    /// <summary>
    /// Escapes a field value for safe inclusion in a CSV file per RFC 4180.
    /// </summary>
    private static string EscapeCsvField(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    /// <summary>
    /// Formats a value as a string for CSV output.
    /// </summary>
    private static string FormatValue(object? value, string? format)
    {
        if (value is null) return string.Empty;

        return value switch
        {
            DateTime dt when !string.IsNullOrEmpty(format) => dt.ToString(format),
            DateTime dt => dt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            DateTimeOffset dto when !string.IsNullOrEmpty(format) => dto.ToString(format),
            DateTimeOffset dto => dto.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            bool b => b ? "Yes" : "No",
            Enum e => e.ToString(),
            _ => value.ToString() ?? string.Empty
        };
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Splits a PascalCase string into words separated by spaces.
    /// </summary>
    private static string SplitPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var sb = new StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            if (i > 0 && char.IsUpper(input[i]) && !char.IsUpper(input[i - 1]))
            {
                sb.Append(' ');
            }
            sb.Append(input[i]);
        }
        return sb.ToString();
    }

    #endregion

    #region Internal Models

    /// <summary>
    /// Represents a column configuration for export.
    /// </summary>
    private sealed class ExportColumn
    {
        public required PropertyInfo Property { get; init; }
        public required string Header { get; init; }
        public required int Order { get; init; }
        public string? Format { get; init; }
        public string? NullText { get; init; }
    }

    #endregion
}
