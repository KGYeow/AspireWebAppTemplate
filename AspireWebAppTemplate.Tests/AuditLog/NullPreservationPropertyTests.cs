// Feature: audit-log-old-new-values, Property 5: Null Values Preserved as JSON Null
using System.Text.Json;
using AspireWebAppTemplate.ApiService.Utilities;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Property-based tests verifying that null values in dictionaries are preserved
/// as JSON null literals in the serialized output, rather than being omitted.
/// </summary>
/// <remarks>
/// **Validates: Requirements 7.5**
/// </remarks>
public class NullPreservationPropertyTests
{
    /// <summary>
    /// Property: For any dictionary containing entries where the value is null,
    /// serializing via AuditChangeHelper.Serialize SHALL produce JSON that includes
    /// those keys with the JSON literal null as their value, rather than omitting
    /// the key entirely. Non-null entries are also preserved correctly alongside null entries.
    /// **Validates: Requirements 7.5**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property NullValues_SerializedAsJsonNull_NotOmitted()
    {
        // Generator for non-empty alphanumeric keys (valid dictionary keys)
        var keyGen = Gen.Elements("Name", "Email", "Phone", "Title", "Department", "Notes", "Status", "Value")
            .SelectMany(prefix => Gen.Choose(1, 999).Select(n => $"{prefix}{n}"));

        // Generator for a dictionary with at least one null value entry and optionally some non-null entries
        var dictWithNullsGen =
            Gen.Choose(1, 5).SelectMany(nullCount =>
            Gen.Choose(0, 3).SelectMany(nonNullCount =>
            {
                // Generate distinct keys for null entries
                var nullKeysGen = Gen.ListOf(keyGen, nullCount)
                    .Select(keys => keys.Distinct().ToList());

                // Generate distinct keys for non-null entries
                var nonNullKeysGen = Gen.ListOf(keyGen, nonNullCount)
                    .Select(keys => keys.Distinct().ToList());

                return nullKeysGen.SelectMany(nullKeys =>
                    nonNullKeysGen.Select(nonNullKeys =>
                    {
                        var dict = new Dictionary<string, object?>();
                        foreach (var key in nullKeys)
                            dict[key] = null;
                        foreach (var key in nonNullKeys)
                        {
                            if (!dict.ContainsKey(key))
                                dict[key] = "someValue";
                        }
                        return dict;
                    }));
            }))
            .Where(d => d.Values.Any(v => v == null) && d.Count > 0);

        return Prop.ForAll(
            Arb.From(dictWithNullsGen),
            (Dictionary<string, object?> dict) =>
            {
                // Act
                var json = AuditChangeHelper.Serialize(dict);

                // The dictionary is non-null so Serialize should return non-null JSON
                if (json == null)
                    return false.Label("Serialize returned null for a non-null dictionary");

                // Parse the JSON output
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Verify: all keys with null values appear in JSON with JsonValueKind.Null
                var nullKeys = dict.Where(kvp => kvp.Value == null).Select(kvp => kvp.Key).ToList();
                foreach (var key in nullKeys)
                {
                    // Key must exist in JSON (not omitted)
                    if (!root.TryGetProperty(key, out var element))
                        return false.Label($"Key '{key}' with null value was omitted from JSON output");

                    // Value must be JSON null literal
                    if (element.ValueKind != JsonValueKind.Null)
                        return false.Label($"Key '{key}' expected JsonValueKind.Null but got {element.ValueKind}");
                }

                // Verify: non-null entries are also preserved correctly
                var nonNullKeys = dict.Where(kvp => kvp.Value != null).Select(kvp => kvp.Key).ToList();
                foreach (var key in nonNullKeys)
                {
                    if (!root.TryGetProperty(key, out var element))
                        return false.Label($"Non-null key '{key}' was omitted from JSON output");

                    if (element.ValueKind == JsonValueKind.Null)
                        return false.Label($"Non-null key '{key}' was serialized as null but had value '{dict[key]}'");
                }

                return true.Label("All null values preserved as JSON null, non-null values preserved correctly");
            });
    }
}
