namespace AspireWebAppTemplate.Core.Common.Defaults;

/// <summary>
/// Default values for data export operations (Excel, CSV).
/// </summary>
public static class ExportDefaults
{
    /// <summary>
    /// Maximum number of rows allowed in a single Excel export.
    /// Prevents memory exhaustion and excessive response times.
    /// </summary>
    public const int MaxExportRows = 100_000;
}
