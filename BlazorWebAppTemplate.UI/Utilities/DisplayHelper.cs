using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace BlazorWebAppTemplate.UI.Utilities;

/// <summary>
/// Reflection-based helper for resolving display names and formatting values
/// from data annotation attributes on model properties.
/// </summary>
/// <remarks>
/// <para>Use this utility to dynamically resolve column headers for data grids
/// and format property values for display or export based on model attributes.</para>
/// <para>Supported attributes:</para>
/// <list type="bullet">
///   <item><see cref="DisplayAttribute"/> — <c>[Display(Name = "...")]</c></item>
///   <item><see cref="DisplayNameAttribute"/> — <c>[DisplayName("...")]</c></item>
///   <item><see cref="DisplayFormatAttribute"/> — <c>[DisplayFormat(DataFormatString = "...")]</c></item>
/// </list>
/// <para>Example:</para>
/// <code>
/// // Resolve column header from [Display] attribute
/// var header = DisplayHelper.GetDisplayName&lt;MyModel&gt;(nameof(MyModel.CreatedAt));
///
/// // Format a value using [DisplayFormat] attribute
/// var formatted = DisplayHelper.FormatValue&lt;MyModel&gt;(item.CreatedAt, nameof(MyModel.CreatedAt));
/// </code>
/// </remarks>
public static class DisplayHelper
{
    /// <summary>
    /// Gets the display name for a property from <see cref="DisplayAttribute"/> or
    /// <see cref="DisplayNameAttribute"/>. Falls back to the raw property name if
    /// no attribute is found.
    /// </summary>
    /// <typeparam name="T">The model type containing the property.</typeparam>
    /// <param name="propertyName">The property name (use <c>nameof</c>).</param>
    /// <returns>The resolved display name string.</returns>
    public static string GetDisplayName<T>(string propertyName)
    {
        var prop = typeof(T).GetProperty(propertyName);
        if (prop is null)
            return propertyName;

        // Check [Display(Name = "...")] first
        var displayAttr = prop.GetCustomAttributes(typeof(DisplayAttribute), true)
                              .FirstOrDefault() as DisplayAttribute;
        if (displayAttr?.Name is not null)
            return displayAttr.Name;

        // Check [DisplayName("...")] as fallback
        var displayNameAttr = prop.GetCustomAttributes(typeof(DisplayNameAttribute), true)
                                  .FirstOrDefault() as DisplayNameAttribute;
        if (displayNameAttr?.DisplayName is not null)
            return displayNameAttr.DisplayName;

        // No attribute found — return raw property name
        return propertyName;
    }

    /// <summary>
    /// Formats a property value using <see cref="DisplayFormatAttribute.DataFormatString"/> if present.
    /// Returns <see cref="string.Empty"/> for null values, or <c>value.ToString()</c> as fallback.
    /// </summary>
    /// <typeparam name="T">The model type containing the property.</typeparam>
    /// <param name="value">The property value to format.</param>
    /// <param name="propertyName">The property name (use <c>nameof</c>).</param>
    /// <returns>The formatted string representation of the value.</returns>
    public static string FormatValue<T>(object? value, string propertyName)
    {
        // Treat null and empty strings as "missing"
        var isMissing = value is null || (value is string s && string.IsNullOrEmpty(s));
        if (isMissing)
            return string.Empty;

        // If there is a [DisplayFormat(DataFormatString = "...")] attribute, use it
        var prop = typeof(T).GetProperty(propertyName);
        if (prop is not null)
        {
            var formatAttr = prop.GetCustomAttributes(typeof(DisplayFormatAttribute), true)
                                 .FirstOrDefault() as DisplayFormatAttribute;

            if (!string.IsNullOrEmpty(formatAttr?.DataFormatString))
            {
                // DataFormatString is typically like "{0:dd/MM/yyyy HH:mm:ss}"
                return string.Format(formatAttr!.DataFormatString, value);
            }
        }

        // Fallback: value.ToString()
        return value?.ToString() ?? string.Empty;
    }
}
