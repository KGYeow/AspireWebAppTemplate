namespace AspireWebAppTemplate.Domain.Enums;

/// <summary>
/// Defines the scope of an exportable property, allowing different export
/// variants to include different subsets of columns.
/// </summary>
public enum ExportScope
{
    /// <summary>Include in all export variants.</summary>
    All = 0,

    /// <summary>Include only in the primary/full export.</summary>
    Primary = 1,

    /// <summary>Include only in the secondary/summary export.</summary>
    Secondary = 2
}
