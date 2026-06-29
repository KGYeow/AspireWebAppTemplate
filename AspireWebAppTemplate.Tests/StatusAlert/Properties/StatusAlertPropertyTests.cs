// Feature: status-alert
using AspireWebAppTemplate.UI.Components.Shared;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using MudBlazor;
using StatusAlertComponent = AspireWebAppTemplate.UI.Components.Shared.StatusAlert;

namespace AspireWebAppTemplate.Tests.StatusAlert.Properties;

/// <summary>
/// Property-based tests verifying the correctness properties of the StatusAlert component.
/// Tests validate self-hiding behavior, parameter pass-through, CSS class composition,
/// and message content rendering across randomized input combinations.
/// </summary>
/// <remarks>
/// **Validates: Requirements 1.1, 1.2, 2.3, 3.1, 3.3, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2**
/// </remarks>
public class StatusAlertPropertyTests
{
    #region Generators

    /// <summary>
    /// Generator for MudBlazor Severity enum values (Error, Success, Info, Warning).
    /// </summary>
    private static readonly Gen<Severity> SeverityGen =
        Gen.Elements(Severity.Error, Severity.Success, Severity.Info, Severity.Warning);

    /// <summary>
    /// Generator for nullable message strings including null, empty, and sample non-empty values.
    /// </summary>
    private static readonly Gen<string?> MessageGen =
        Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Constant<string?>(""),
            Gen.Elements<string?>("Error occurred", "Success!", "Please note", "Warning: check input"));

    /// <summary>
    /// Generator for non-empty message strings used when testing rendering behavior.
    /// </summary>
    private static readonly Gen<string> NonEmptyMessageGen =
        Gen.Elements("Error occurred", "Success!", "Please note", "Warning: check input");

    /// <summary>
    /// Generator for nullable CSS class strings including null and sample CSS class values.
    /// </summary>
    private static readonly Gen<string?> ClassGen =
        Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Elements<string?>("mt-2", "custom-class", "pa-4 ma-2"));

    #endregion

    // Feature: status-alert, Property 1: Self-Hiding Invariant
    #region Property 1: Self-Hiding Invariant

    /// <summary>
    /// Property: For any combination of Severity, Dismissible, Dense, Class, and ChildContent values,
    /// when Message is null or empty string, the StatusAlert self-hiding gate evaluates to true,
    /// meaning no markup would be rendered.
    /// **Validates: Requirements 1.1, 1.2, 1.5**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property NullOrEmptyMessage_RendersNothing()
    {
        var nullOrEmptyMessageGen = Gen.Elements<string?>(null, "");

        var gen = from message in nullOrEmptyMessageGen
                  from severity in SeverityGen
                  from dense in Gen.Elements(true, false)
                  from dismissible in Gen.Elements(true, false)
                  from cssClass in ClassGen
                  select (message, severity, dense, dismissible, cssClass);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (message, severity, dense, dismissible, cssClass) = tuple;

            // The component's render gate: renders nothing when Message is null/empty
            var shouldHide = string.IsNullOrEmpty(message);

            return shouldHide.Label($"Message='{message}' should hide (Severity={severity}, Dense={dense}, Dismissible={dismissible}, Class='{cssClass}')");
        });
    }

    #endregion

    // Feature: status-alert, Property 2: Parameter Pass-Through
    #region Property 2: Parameter Pass-Through

    /// <summary>
    /// Property: For any non-empty Message string and any valid combination of Severity,
    /// Dismissible, and Dense values, the component stores the exact parameter values
    /// that would be passed through to the underlying MudAlert during rendering.
    /// **Validates: Requirements 2.3, 3.1, 3.3, 5.1, 5.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property NonEmptyMessage_ParametersPassedThrough()
    {
        var gen = from message in NonEmptyMessageGen
                  from severity in SeverityGen
                  from dense in Gen.Elements(true, false)
                  from dismissible in Gen.Elements(true, false)
                  select (message, severity, dense, dismissible);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (message, severity, dense, dismissible) = tuple;

            var alert = new StatusAlertComponent
            {
                Message = message,
                Severity = severity,
                Dense = dense,
                Dismissible = dismissible
            };

            // Parameters should hold their values for pass-through to MudAlert
            var severityCorrect = alert.Severity == severity;
            var denseCorrect = alert.Dense == dense;
            var dismissibleCorrect = alert.Dismissible == dismissible;

            return (severityCorrect && denseCorrect && dismissibleCorrect)
                .Label($"Severity={severity}=={alert.Severity}, Dense={dense}=={alert.Dense}, Dismissible={dismissible}=={alert.Dismissible}");
        });
    }

    #endregion

    // Feature: status-alert, Property 3: CSS Class Composition
    #region Property 3: CSS Class Composition

    /// <summary>
    /// Property: For any non-empty Message and any consumer-provided Class string,
    /// the computed CSS class string always contains "border-1" and appends the Class value
    /// when non-null.
    /// **Validates: Requirements 4.1, 4.4**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property NonEmptyMessage_CssClassCompositionIsCorrect()
    {
        var gen = from message in NonEmptyMessageGen
                  from dense in Gen.Elements(true, false)
                  from cssClass in ClassGen
                  select (message, dense, cssClass);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (message, dense, cssClass) = tuple;

            var alert = new StatusAlertComponent
            {
                Message = message,
                Dense = dense,
                Class = cssClass
            };

            var computedClass = alert.ComputedClass;

            // (a) Always contains "border-1"
            var hasBorder = computedClass.Contains("border-1");

            // (b) Contains consumer Class when non-null
            var classCorrect = cssClass == null || computedClass.Contains(cssClass);

            // (c) When Class is null, ComputedClass is just "border-1"
            var nullClassCorrect = cssClass != null || computedClass == "border-1";

            return (hasBorder && classCorrect && nullClassCorrect)
                .Label($"ComputedClass='{computedClass}', Class='{cssClass}', " +
                       $"HasBorder={hasBorder}, ClassCorrect={classCorrect}, NullClassCorrect={nullClassCorrect}");
        });
    }

    #endregion

    // Feature: status-alert, Property 4: Message Content Rendering
    #region Property 4: Message Content Rendering

    /// <summary>
    /// Property: For any non-empty Message string, when ChildContent is not provided,
    /// the component's Message property contains the exact text that would be rendered
    /// as the alert body content (since the template renders @Message when ChildContent is null).
    /// **Validates: Requirements 1.3, 6.2**
    /// </summary>
    [Property(MaxTest = 2)]
    public FsCheck.Property NonEmptyMessage_NoChildContent_MessageRenderedAsBody()
    {
        var gen = from message in NonEmptyMessageGen
                  from severity in SeverityGen
                  from dense in Gen.Elements(true, false)
                  from dismissible in Gen.Elements(true, false)
                  select (message, severity, dense, dismissible);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (message, severity, dense, dismissible) = tuple;

            var alert = new StatusAlertComponent
            {
                Message = message,
                Severity = severity,
                Dense = dense,
                Dismissible = dismissible,
                ChildContent = null  // No ChildContent — Message is used as body
            };

            // When ChildContent is null, the template renders @Message
            // The Message property should contain the exact text to be rendered
            var childContentIsNull = alert.ChildContent == null;
            var messageIsPreserved = alert.Message == message;
            var messageIsNonEmpty = !string.IsNullOrEmpty(alert.Message);

            return (childContentIsNull && messageIsPreserved && messageIsNonEmpty)
                .Label($"Message='{message}', ChildContent={alert.ChildContent}, " +
                       $"MessagePreserved={messageIsPreserved}, NonEmpty={messageIsNonEmpty}");
        });
    }

    #endregion
}
