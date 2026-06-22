using System.Text.Json;

namespace AspireWebAppTemplate.ApiService.Utilities;

/// <summary>
/// Provides helper methods for computing change sets and serializing
/// old/new values for audit log entries.
/// </summary>
public static class AuditChangeHelper
{
    /// <summary>
    /// Shared <see cref="JsonSerializerOptions"/> instance configured for audit log JSON output.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>
    ///     <term>CamelCase naming</term>
    ///     <description>
    ///       Uses <see cref="JsonNamingPolicy.CamelCase"/> so that PascalCase C# property names
    ///       (e.g., <c>IsActive</c>, <c>DisplayName</c>) are serialized as camelCase JSON keys
    ///       (e.g., <c>isActive</c>, <c>displayName</c>). Note: this policy applies only to
    ///       typed object properties — dictionary keys are passed through unchanged.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Never ignore nulls</term>
    ///     <description>
    ///       Uses <see cref="System.Text.Json.Serialization.JsonIgnoreCondition.Never"/> to ensure
    ///       null property values are serialized as the JSON literal <c>null</c> rather than being
    ///       omitted from the output. This makes it possible to distinguish "field was cleared to null"
    ///       from "field was not part of the change set".
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    /// <summary>
    /// Creates a snapshot dictionary from an entity using a predefined field list.
    /// Eliminates repetitive dictionary construction in controller actions.
    /// </summary>
    public static Dictionary<string, object?> Snapshot<T>(T entity, params (string Key, Func<T, object?> Getter)[] fields)
    {
        return fields.ToDictionary(f => f.Key, f => f.Getter(entity));
    }

    /// <summary>
    /// Computes the diff between two dictionaries and returns serialized JSON
    /// for the old and new values containing only the changed fields.
    /// Returns (null, null) if no fields changed.
    /// </summary>
    public static (string? OldValues, string? NewValues) ComputeChanges(Dictionary<string, object?> before, Dictionary<string, object?> after)
    {
        var oldDiff = new Dictionary<string, object?>();
        var newDiff = new Dictionary<string, object?>();

        foreach (var key in before.Keys)
        {
            var oldVal = before[key];
            var newVal = after.GetValueOrDefault(key);

            if (!Equals(oldVal, newVal))
            {
                oldDiff[key] = oldVal;
                newDiff[key] = newVal;
            }
        }

        if (oldDiff.Count == 0)
            return (null, null);

        return (
            JsonSerializer.Serialize(oldDiff, CamelCaseOptions),
            JsonSerializer.Serialize(newDiff, CamelCaseOptions)
        );
    }

    /// <summary>
    /// Serializes an object to camelCase JSON for use in OldValues/NewValues.
    /// Returns null if the value is null.
    /// </summary>
    public static string? Serialize(object? value)
    {
        if (value is null) return null;
        return JsonSerializer.Serialize(value, CamelCaseOptions);
    }
}
