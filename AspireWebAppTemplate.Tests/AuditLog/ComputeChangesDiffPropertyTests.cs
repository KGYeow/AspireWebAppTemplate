// Feature: audit-log-old-new-values, Property 2: ComputeChanges Includes Only and All Differing Fields
using System.Text.Json;
using AspireWebAppTemplate.ApiService.Utilities;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Property-based tests verifying that ComputeChanges returns JSON containing exactly
/// the keys whose values differ between two dictionaries — no more, no fewer.
/// </summary>
/// <remarks>
/// **Validates: Requirements 3.1, 3.2, 4.1, 4.2, 6.1, 6.2, 6.3, 7.2, 7.3**
/// </remarks>
public class ComputeChangesDiffPropertyTests
{
    private static readonly string[] FieldNames =
        ["Name", "Email", "Phone", "Title", "Department", "Status", "Active", "Score", "Role", "Level"];

    private static readonly string[] SampleValues =
        ["Alice", "Bob", "charlie@test.com", "Manager", "IT", "true", "false", "42", "100", ""];

    /// <summary>
    /// Generates a pair of dictionaries (before, after) with the same keys but potentially different values.
    /// </summary>
    private static Gen<(Dictionary<string, object?>, Dictionary<string, object?>)> DictPairGen()
    {
        // Pick how many fields to include (1-5)
        var sizeGen = Gen.Choose(1, 5);
        var valueIdxGen = Gen.Choose(0, SampleValues.Length); // index = SampleValues.Length means null

        // Generate a pair of value-index arrays (before indices, after indices) for a given size
        return sizeGen.SelectMany<int, (Dictionary<string, object?>, Dictionary<string, object?>)>(size =>
        {
            var beforeIndicesGen = Gen.ListOf(valueIdxGen, size);
            var afterIndicesGen = Gen.ListOf(valueIdxGen, size);

            return beforeIndicesGen.SelectMany(beforeIndices =>
                afterIndicesGen.Select(afterIndices =>
                {
                    var before = new Dictionary<string, object?>();
                    var after = new Dictionary<string, object?>();
                    var bList = beforeIndices.ToList();
                    var aList = afterIndices.ToList();
                    for (int i = 0; i < size; i++)
                    {
                        var key = FieldNames[i];
                        before[key] = bList[i] < SampleValues.Length ? SampleValues[bList[i]] : null;
                        after[key] = aList[i] < SampleValues.Length ? SampleValues[aList[i]] : null;
                    }
                    return ((Dictionary<string, object?>)before, (Dictionary<string, object?>)after);
                }));
        });
    }

    /// <summary>
    /// Property: For any two dictionaries representing before-state and after-state field snapshots,
    /// ComputeChanges SHALL return JSON containing exactly the keys whose values differ between the
    /// two dictionaries — no more, no fewer. If both values are equal for a key, that key SHALL NOT
    /// appear in the output. If no keys differ, the output SHALL be (null, null).
    /// Every differing key appears in BOTH OldValues and NewValues.
    /// **Validates: Requirements 3.1, 3.2, 4.1, 4.2, 6.1, 6.2, 6.3, 7.2, 7.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ComputeChanges_ReturnsExactlyDifferingKeys()
    {
        return Prop.ForAll(
            Arb.From(DictPairGen()),
            ((Dictionary<string, object?> before, Dictionary<string, object?> after) pair) =>
            {
                var (before, after) = pair;

                // Compute the expected set of differing keys
                var expectedDifferingKeys = new HashSet<string>();
                foreach (var key in before.Keys)
                {
                    var oldVal = before[key];
                    var newVal = after.GetValueOrDefault(key);
                    if (!Equals(oldVal, newVal))
                    {
                        expectedDifferingKeys.Add(key);
                    }
                }

                // Call the method under test
                var (oldValues, newValues) = AuditChangeHelper.ComputeChanges(before, after);

                // If no keys differ, output SHALL be (null, null)
                if (expectedDifferingKeys.Count == 0)
                {
                    var bothNull = oldValues == null && newValues == null;
                    return bothNull.Label("Expected (null, null) when no keys differ, " +
                        $"but got OldValues={oldValues}, NewValues={newValues}");
                }

                // Both must be non-null when there are differences
                if (oldValues == null || newValues == null)
                {
                    return false.Label($"Expected non-null JSON when {expectedDifferingKeys.Count} keys differ, " +
                        $"but got OldValues={oldValues}, NewValues={newValues}");
                }

                // Parse the JSON outputs
                using var oldDoc = JsonDocument.Parse(oldValues);
                using var newDoc = JsonDocument.Parse(newValues);

                var oldKeys = new HashSet<string>(oldDoc.RootElement.EnumerateObject().Select(p => p.Name));
                var newKeys = new HashSet<string>(newDoc.RootElement.EnumerateObject().Select(p => p.Name));

                // ComputeChanges preserves dictionary keys as-is in the JSON output
                // (System.Text.Json's PropertyNamingPolicy applies to object properties, not dictionary keys)
                // 1. Only keys with differing values appear in OldValues/NewValues JSON
                var oldKeysMatchExpected = oldKeys.SetEquals(expectedDifferingKeys);
                // 2. Every differing key appears in BOTH OldValues and NewValues
                var newKeysMatchExpected = newKeys.SetEquals(expectedDifferingKeys);
                // 4. Both outputs have the same keys
                var keysMatch = oldKeys.SetEquals(newKeys);

                var allPass = oldKeysMatchExpected && newKeysMatchExpected && keysMatch;

                return allPass.Label(
                    $"Key mismatch. Expected keys: [{string.Join(", ", expectedDifferingKeys)}], " +
                    $"OldValues keys: [{string.Join(", ", oldKeys)}], " +
                    $"NewValues keys: [{string.Join(", ", newKeys)}]");
            });
    }
}
