using System;
using AspireWebAppTemplate.Core.Domain.Enums;

namespace AspireWebAppTemplate.Core.Utilities.Attributes;

/// <summary>
/// Marks a property as a column in Excel/CSV exports.
/// Only properties decorated with this attribute will be included in generated exports.
/// </summary>
/// <remarks>
/// <para>Decorate model properties with this attribute to control which columns
/// appear in exported files and in what order.</para>
/// <para>Example:</para>
/// <code>
/// public class ReportItem
/// {
///     [ExportColumn(1)]
///     [Display(Name = "Serial Number")]
///     public string SerialNumber { get; set; }
///
///     [ExportColumn(2, ExportScope.Primary)]
///     [Display(Name = "Manufacturing Date")]
///     [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}")]
///     public DateTime ManufacturingDate { get; set; }
///
///     [ExportColumn(3, NullText = "N/A")]
///     public string? Notes { get; set; }
///
///     // Not exported — no [ExportColumn] attribute
///     public int InternalId { get; set; }
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ExportColumnAttribute : Attribute
{
    /// <summary>
    /// The column order in the exported file (lower values appear earlier).
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Which export variant(s) this property belongs to.
    /// Defaults to <see cref="ExportScope.All"/>.
    /// </summary>
    public ExportScope Scope { get; }

    /// <summary>
    /// Optional text to display when the property value is null.
    /// Defaults to <c>null</c> (empty cell in export).
    /// </summary>
    public string? NullText { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="ExportColumnAttribute"/>.
    /// </summary>
    /// <param name="order">The column order in the exported file.</param>
    /// <param name="scope">Which export variant(s) to include this property in.</param>
    public ExportColumnAttribute(int order, ExportScope scope = ExportScope.All)
    {
        Order = order;
        Scope = scope;
    }
}
