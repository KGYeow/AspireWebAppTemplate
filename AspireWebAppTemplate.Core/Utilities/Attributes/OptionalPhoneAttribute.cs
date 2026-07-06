using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace AspireWebAppTemplate.Core.Utilities.Attributes;

/// <summary>
/// Validates a phone number field that is optional — null, empty, and whitespace-only
/// values are always considered valid (allowing the user to clear the field).
/// Non-empty values are validated against a permissive phone number pattern.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class OptionalPhoneAttribute : ValidationAttribute
{
    private static readonly Regex PhoneRegex = new(@"^\+?[\d\s\-\(\)\.]+$", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of <see cref="OptionalPhoneAttribute"/>
    /// with a default error message.
    /// </summary>
    public OptionalPhoneAttribute() : base("The {0} field is not a valid phone number.")
    {
    }

    /// <inheritdoc />
    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        if (value is not string stringValue)
            return false;

        if (string.IsNullOrWhiteSpace(stringValue))
            return true;

        return PhoneRegex.IsMatch(stringValue);
    }
}
