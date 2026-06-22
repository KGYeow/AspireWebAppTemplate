// Feature: audit-log-old-new-values, Property 4: CamelCase Naming in Serialized Output
using System.Text.Json;
using AspireWebAppTemplate.ApiService.Utilities;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspireWebAppTemplate.Tests.AuditLog;

/// <summary>
/// Property-based tests verifying that AuditChangeHelper.Serialize produces
/// camelCase property names when serializing typed objects with PascalCase properties.
/// </summary>
/// <remarks>
/// **Validates: Requirements 7.1**
/// </remarks>
public class CamelCaseNamingPropertyTests
{
    /// <summary>
    /// A simple record with PascalCase property names used to verify
    /// that serialization converts them to camelCase in the JSON output.
    /// </summary>
    private record AuditableEntity(
        string DisplayName,
        bool IsActive,
        string[] PagePaths,
        int Position,
        string? Description);

    /// <summary>
    /// Property: For any typed object with PascalCase properties, serializing via
    /// AuditChangeHelper.Serialize SHALL produce JSON where every property name is
    /// the camelCase equivalent (first character lowercased) of the original property name.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property TypedObject_PropertiesSerializedAsCamelCase()
    {
        var displayNameGen = Gen.Elements("Alice", "Bob", "Charlie", "Test User");
        var isActiveGen = Gen.Elements(true, false);
        var pagePathsGen = Gen.Elements(
            new[] { "/admin", "/dashboard" },
            new[] { "/settings" },
            Array.Empty<string>());
        var positionGen = Gen.Choose(0, 100);
        var descriptionGen = Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Constant<string?>("A description"));

        var entityGen = from name in displayNameGen
                        from active in isActiveGen
                        from paths in pagePathsGen
                        from pos in positionGen
                        from desc in descriptionGen
                        select new AuditableEntity(name, active, paths, pos, desc);

        return Prop.ForAll(Arb.From(entityGen), (AuditableEntity entity) =>
        {
            // Act
            var json = AuditChangeHelper.Serialize(entity);

            // Parse the JSON and inspect property names
            using var doc = JsonDocument.Parse(json!);
            var root = doc.RootElement;

            var expectedCamelCaseNames = new[] { "displayName", "isActive", "pagePaths", "position", "description" };

            var allPropertiesAreCamelCase = expectedCamelCaseNames
                .All(name => root.TryGetProperty(name, out _));

            // Also verify no PascalCase names exist
            var pascalCaseNames = new[] { "DisplayName", "IsActive", "PagePaths", "Position", "Description" };
            var noPascalCaseNames = pascalCaseNames
                .All(name => !root.TryGetProperty(name, out _));

            // Verify the total number of properties matches (no unexpected properties)
            var propertyCount = root.EnumerateObject().Count();
            var correctCount = propertyCount == expectedCamelCaseNames.Length;

            return (allPropertiesAreCamelCase && noPascalCaseNames && correctCount)
                .Label($"JSON={json}, AllCamelCase={allPropertiesAreCamelCase}, " +
                       $"NoPascalCase={noPascalCaseNames}, CorrectCount={correctCount} (got {propertyCount})");
        });
    }
}
