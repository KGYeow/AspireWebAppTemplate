// Feature: audit-log-old-new-values, Property 3: Serialization Round-Trip Preserves Values
using System.Text.Json;
using AspireWebAppTemplate.ApiService.Utilities;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Property-based tests verifying that serializing a dictionary via AuditChangeHelper.Serialize
/// and deserializing the resulting JSON recovers equivalent key-value pairs.
/// </summary>
/// <remarks>
/// **Validates: Requirements 3.5, 4.3, 4.4, 5.1, 7.1**
/// </remarks>
public class SerializationRoundTripPropertyTests
{
    /// <summary>
    /// Generates a random primitive value (string, bool, int, or null) suitable for
    /// dictionary values that round-trip cleanly through System.Text.Json.
    /// </summary>
    private static Gen<object?> PrimitiveValueGen()
    {
        var stringGen = Gen.Elements<object?>("hello", "world", "", "test value", "123", null);
        var boolGen = Gen.Elements<object?>(true, false);
        var intGen = Gen.Choose(-1000, 1000).Select(i => (object?)i);
        var nullGen = Gen.Constant<object?>(null);

        return Gen.OneOf(stringGen, boolGen, intGen, nullGen);
    }

    /// <summary>
    /// Generates a non-empty dictionary key (avoids empty strings which are valid but
    /// could complicate JSON property naming).
    /// </summary>
    private static Gen<string> KeyGen()
    {
        return Gen.Elements(
            "DisplayName", "FirstName", "LastName", "Email",
            "PhoneNumber", "IsActive", "Department", "Roles",
            "PagePaths", "Theme", "JobTitle", "Position");
    }

    /// <summary>
    /// Generates a Dictionary&lt;string, object?&gt; with 1-5 entries using
    /// known keys and random primitive values.
    /// </summary>
    private static Gen<Dictionary<string, object?>> DictionaryGen()
    {
        return Gen.Choose(1, 5).SelectMany(count =>
        {
            // Pick 'count' unique keys and pair each with a random value
            return KeyGen()
                .ListOf(count)
                .Select(keys => keys.Distinct().ToList())
                .SelectMany(uniqueKeys =>
                {
                    // Build a single generator that produces a dictionary
                    // by generating a value for each key
                    return PrimitiveValueGen()
                        .ListOf(uniqueKeys.Count)
                        .Select(values =>
                        {
                            var dict = new Dictionary<string, object?>();
                            for (int i = 0; i < uniqueKeys.Count; i++)
                            {
                                dict[uniqueKeys[i]] = values[i];
                            }
                            return dict;
                        });
                });
        });
    }

    /// <summary>
    /// Compares two values at the semantic level, accounting for System.Text.Json
    /// deserializing numbers as JsonElement.
    /// </summary>
    private static bool ValuesAreEquivalent(object? original, JsonElement element)
    {
        if (original is null)
            return element.ValueKind == JsonValueKind.Null;

        return original switch
        {
            string s => element.ValueKind == JsonValueKind.String && element.GetString() == s,
            bool b => (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
                      && element.GetBoolean() == b,
            int i => element.ValueKind == JsonValueKind.Number && element.GetInt32() == i,
            _ => element.ToString() == original.ToString()
        };
    }

    /// <summary>
    /// Property: For any dictionary of string keys to nullable object values, serializing via
    /// AuditChangeHelper.Serialize and then deserializing the resulting JSON back into a dictionary
    /// SHALL produce a dictionary with equivalent key-value pairs (accounting for numeric type
    /// normalization in System.Text.Json).
    ///
    /// Key behaviors verified:
    /// 1. Serializing a Dictionary&lt;string, object?&gt; via Serialize() produces valid JSON
    /// 2. Deserializing that JSON back recovers the same key-value pairs
    /// 3. String values round-trip exactly
    /// 4. System.Text.Json deserializes numbers as JsonElement, so compare at semantic level
    ///
    /// **Validates: Requirements 3.5, 4.3, 4.4, 5.1, 7.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property Serialize_RoundTrip_PreservesValues()
    {
        var dictArb = Arb.From(DictionaryGen());

        return Prop.ForAll(dictArb, (Dictionary<string, object?> input) =>
        {
            // Act: Serialize via AuditChangeHelper
            var json = AuditChangeHelper.Serialize(input);

            // The result should not be null for a non-null input
            if (json is null)
                return false.Label("Serialize returned null for non-null input");

            // Deserialize back using System.Text.Json
            var deserialized = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

            if (deserialized is null)
                return false.Label("Deserialization returned null");

            // Verify same number of keys
            if (deserialized.Count != input.Count)
                return false.Label(
                    $"Key count mismatch. Expected={input.Count}, Actual={deserialized.Count}");

            // Verify each key-value pair round-trips correctly
            foreach (var kvp in input)
            {
                if (!deserialized.ContainsKey(kvp.Key))
                    return false.Label($"Missing key '{kvp.Key}' after round-trip");

                var element = deserialized[kvp.Key];
                if (!ValuesAreEquivalent(kvp.Value, element))
                    return false.Label(
                        $"Value mismatch for key '{kvp.Key}'. " +
                        $"Original={kvp.Value ?? "null"} ({kvp.Value?.GetType().Name ?? "null"}), " +
                        $"Deserialized={element}");
            }

            return true.Label("Round-trip preserves all values");
        });
    }
}
