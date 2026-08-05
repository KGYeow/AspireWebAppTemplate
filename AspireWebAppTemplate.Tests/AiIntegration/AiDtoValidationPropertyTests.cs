// Feature: aws-ai-integration, Property 5: DTO validation enforces prompt constraints
using System.ComponentModel.DataAnnotations;
using AspireWebAppTemplate.Application.Contracts.Ai;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace AspireWebAppTemplate.Tests.AiIntegration;

/// <summary>
/// Property-based tests verifying AiPromptRequest DTO validation enforces prompt constraints.
/// Validation succeeds if and only if the Prompt is non-null, non-empty, and at most 4000 characters.
/// </summary>
/// <remarks>
/// **Validates: Requirements 5.1, 5.3**
/// </remarks>
public class AiDtoValidationPropertyTests
{
    /// <summary>
    /// Validates an AiPromptRequest using DataAnnotations and returns whether validation succeeded.
    /// </summary>
    private static bool ValidateRequest(AiPromptRequest request)
    {
        var context = new ValidationContext(request, serviceProvider: null, items: null);
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(request, context, results, validateAllProperties: true);
    }

    /// <summary>
    /// Property: For any non-empty string of 1–4000 characters, Validator.TryValidateObject
    /// SHALL succeed on AiPromptRequest.
    /// **Validates: Requirements 5.1, 5.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property ValidPrompt_PassesValidation()
    {
        var charGen = Gen.Elements(
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
            'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
            ' ', '1', '2', '3', '!', '?', '.', ',');
        var validPromptGen = Gen.Choose(1, 4000)
            .SelectMany<int, string>(len => Gen.ArrayOf<char>(charGen, len).Select(chars => new string(chars)))
            .Where(s => !string.IsNullOrEmpty(s));

        return Prop.ForAll(Arb.From(validPromptGen), (string prompt) =>
        {
            var request = new AiPromptRequest { Prompt = prompt };
            var isValid = ValidateRequest(request);
            return isValid.Label($"Expected valid for prompt of length {prompt.Length}, got invalid");
        });
    }

    /// <summary>
    /// Property: For an empty string, Validator.TryValidateObject SHALL fail on AiPromptRequest
    /// due to the [Required] attribute.
    /// **Validates: Requirements 5.1, 5.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property EmptyPrompt_FailsValidation()
    {
        var emptyGen = Gen.Constant(string.Empty);

        return Prop.ForAll(Arb.From(emptyGen), (string prompt) =>
        {
            var request = new AiPromptRequest { Prompt = prompt };
            var isValid = ValidateRequest(request);
            return (!isValid).Label("Expected validation to fail for empty string");
        });
    }

    /// <summary>
    /// Property: For any string exceeding 4000 characters, Validator.TryValidateObject SHALL fail
    /// on AiPromptRequest due to the [StringLength(4000)] attribute.
    /// **Validates: Requirements 5.1, 5.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property OverlengthPrompt_FailsValidation()
    {
        var charGen = Gen.Elements(
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
            'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z');
        var overlengthGen = Gen.Choose(4001, 5000)
            .SelectMany<int, string>(len => Gen.ArrayOf<char>(charGen, len).Select(chars => new string(chars)));

        return Prop.ForAll(Arb.From(overlengthGen), (string prompt) =>
        {
            var request = new AiPromptRequest { Prompt = prompt };
            var isValid = ValidateRequest(request);
            return (!isValid).Label($"Expected validation to fail for prompt of length {prompt.Length}");
        });
    }

    /// <summary>
    /// Property: For a null Prompt value, Validator.TryValidateObject SHALL fail on AiPromptRequest
    /// due to the [Required] attribute.
    /// **Validates: Requirements 5.1, 5.3**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property NullPrompt_FailsValidation()
    {
        var nullGen = Gen.Constant<string?>(null);

        return Prop.ForAll(Arb.From(nullGen), (string? prompt) =>
        {
            var request = new AiPromptRequest { Prompt = prompt! };
            var isValid = ValidateRequest(request);
            return (!isValid).Label("Expected validation to fail for null prompt");
        });
    }
}
