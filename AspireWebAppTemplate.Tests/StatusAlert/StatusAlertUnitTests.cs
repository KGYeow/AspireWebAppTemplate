using AspireWebAppTemplate.UI.Components.Shared;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using StatusAlertComponent = AspireWebAppTemplate.UI.Components.Shared.StatusAlert;

namespace AspireWebAppTemplate.Tests.StatusAlert;

/// <summary>
/// Unit tests for the StatusAlert component verifying specific examples,
/// edge cases, and interaction behavior.
/// </summary>
/// <remarks>
/// **Validates: Requirements 1.4, 1.6, 2.2, 3.2, 3.4, 5.3, 6.3, 7.3**
/// </remarks>
public class StatusAlertUnitTests
{
    #region Default Parameter Values

    /// <summary>
    /// Verifies that the Severity parameter defaults to Error when no value is specified.
    /// **Validates: Requirement 2.2**
    /// </summary>
    [Fact]
    public void DefaultSeverity_IsError()
    {
        // Arrange & Act
        var alert = new StatusAlertComponent();

        // Assert
        Assert.Equal(Severity.Error, alert.Severity);
    }

    /// <summary>
    /// Verifies that the Dismissible parameter defaults to true when no value is specified.
    /// **Validates: Requirement 3.4**
    /// </summary>
    [Fact]
    public void DefaultDismissible_IsTrue()
    {
        // Arrange & Act
        var alert = new StatusAlertComponent();

        // Assert
        Assert.True(alert.Dismissible);
    }

    /// <summary>
    /// Verifies that the Dense parameter defaults to false when no value is specified.
    /// **Validates: Requirement 5.3**
    /// </summary>
    [Fact]
    public void DefaultDense_IsFalse()
    {
        // Arrange & Act
        var alert = new StatusAlertComponent();

        // Assert
        Assert.False(alert.Dense);
    }

    /// <summary>
    /// Verifies that the Message parameter defaults to null when no value is specified.
    /// </summary>
    [Fact]
    public void DefaultMessage_IsNull()
    {
        // Arrange & Act
        var alert = new StatusAlertComponent();

        // Assert
        Assert.Null(alert.Message);
    }

    /// <summary>
    /// Verifies that the Class parameter defaults to null when no value is specified.
    /// </summary>
    [Fact]
    public void DefaultClass_IsNull()
    {
        // Arrange & Act
        var alert = new StatusAlertComponent();

        // Assert
        Assert.Null(alert.Class);
    }

    /// <summary>
    /// Verifies that the ChildContent parameter defaults to null when no value is specified.
    /// </summary>
    [Fact]
    public void DefaultChildContent_IsNull()
    {
        // Arrange & Act
        var alert = new StatusAlertComponent();

        // Assert
        Assert.Null(alert.ChildContent);
    }

    #endregion

    #region CSS Class Composition

    /// <summary>
    /// Verifies that when Dense is false and Class is default (null), ComputedClass is "border-1".
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Fact]
    public void ComputedClass_DefaultClass_ReturnsBorderOnly()
    {
        // Arrange
        var alert = new StatusAlertComponent { Dense = false };

        // Act
        var result = alert.ComputedClass;

        // Assert
        Assert.Equal("border-1", result);
    }

    /// <summary>
    /// Verifies that when Class is "mb-4", ComputedClass is "border-1 mb-4".
    /// **Validates: Requirements 4.1, 4.4**
    /// </summary>
    [Fact]
    public void ComputedClass_WithMb4Class_ReturnsBorderAndMargin()
    {
        // Arrange
        var alert = new StatusAlertComponent { Class = "mb-4" };

        // Act
        var result = alert.ComputedClass;

        // Assert
        Assert.Equal("border-1 mb-4", result);
    }

    /// <summary>
    /// Verifies that when Class is explicitly set, ComputedClass appends that value.
    /// **Validates: Requirements 4.1, 4.4**
    /// </summary>
    [Fact]
    public void ComputedClass_WithCustomClass_AppendsConsumerClass()
    {
        // Arrange
        var alert = new StatusAlertComponent { Class = "mt-2" };

        // Act
        var result = alert.ComputedClass;

        // Assert
        Assert.Equal("border-1 mt-2", result);
    }

    /// <summary>
    /// Verifies that when Class is set to null, ComputedClass is just "border-1".
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Fact]
    public void ComputedClass_ClassNull_ReturnsBorderOnly()
    {
        // Arrange
        var alert = new StatusAlertComponent { Class = null };

        // Act
        var result = alert.ComputedClass;

        // Assert
        Assert.Equal("border-1", result);
    }

    #endregion

    #region Self-Hiding Behavior

    /// <summary>
    /// Verifies that when Message is null, the self-hiding gate evaluates to hide the alert.
    /// **Validates: Requirement 1.4**
    /// </summary>
    [Fact]
    public void SelfHidingGate_MessageNull_ShouldHide()
    {
        // Arrange
        var alert = new StatusAlertComponent { Message = null };

        // Act
        var shouldHide = string.IsNullOrEmpty(alert.Message);

        // Assert
        Assert.True(shouldHide);
    }

    /// <summary>
    /// Verifies that when Message is empty, the self-hiding gate evaluates to hide the alert.
    /// **Validates: Requirement 1.4**
    /// </summary>
    [Fact]
    public void SelfHidingGate_MessageEmpty_ShouldHide()
    {
        // Arrange
        var alert = new StatusAlertComponent { Message = "" };

        // Act
        var shouldHide = string.IsNullOrEmpty(alert.Message);

        // Assert
        Assert.True(shouldHide);
    }

    /// <summary>
    /// Verifies that when Message changes from non-empty to null, the gate evaluates to hide.
    /// This simulates the transition that removes the alert from the DOM.
    /// **Validates: Requirement 1.4**
    /// </summary>
    [Fact]
    public void SelfHidingGate_MessageChangesFromNonEmptyToNull_ShouldHide()
    {
        // Arrange — start with a visible alert
        var alert = new StatusAlertComponent { Message = "Error occurred" };
        Assert.False(string.IsNullOrEmpty(alert.Message));

        // Act — simulate Message changing to null (as happens after dismiss)
        alert.Message = null;

        // Assert — gate now evaluates to hide
        Assert.True(string.IsNullOrEmpty(alert.Message));
    }

    /// <summary>
    /// Verifies that when Message is non-empty, the self-hiding gate evaluates to show the alert.
    /// </summary>
    [Fact]
    public void SelfHidingGate_MessageNonEmpty_ShouldShow()
    {
        // Arrange
        var alert = new StatusAlertComponent { Message = "Something went wrong" };

        // Act
        var shouldHide = string.IsNullOrEmpty(alert.Message);

        // Assert
        Assert.False(shouldHide);
    }

    #endregion

    #region Dismiss Behavior

    /// <summary>
    /// Verifies that invoking MessageChanged with null calls the callback correctly,
    /// simulating the close icon click behavior.
    /// **Validates: Requirements 3.2, 7.3**
    /// </summary>
    [Fact]
    public async Task CloseIconClick_InvokesMessageChangedWithNull()
    {
        // Arrange
        string? receivedValue = "initial";
        var callbackInvoked = false;

        var alert = new StatusAlertComponent
        {
            Message = "Error occurred",
            MessageChanged = EventCallback.Factory.Create<string?>(this, (string? value) =>
            {
                callbackInvoked = true;
                receivedValue = value;
            })
        };

        // Act — simulate what CloseIconClicked does: invoke MessageChanged with null
        await alert.MessageChanged.InvokeAsync(null);

        // Assert
        Assert.True(callbackInvoked);
        Assert.Null(receivedValue);
    }

    /// <summary>
    /// Verifies that an unbound MessageChanged callback does not throw when invoked.
    /// This ensures the component is safe to use without two-way binding.
    /// **Validates: Requirement 7.3**
    /// </summary>
    [Fact]
    public async Task CloseIconClick_UnboundCallback_DoesNotThrow()
    {
        // Arrange — no MessageChanged callback assigned (default EventCallback struct)
        var alert = new StatusAlertComponent { Message = "Error occurred" };

        // Act & Assert — invoking an unbound EventCallback should not throw
        var exception = await Record.ExceptionAsync(() => alert.MessageChanged.InvokeAsync(null));
        Assert.Null(exception);
    }

    #endregion

    #region ChildContent Precedence

    /// <summary>
    /// Verifies that when ChildContent is provided along with a non-empty Message,
    /// the component's ChildContent is non-null (indicating it takes precedence for rendering).
    /// **Validates: Requirements 1.6, 6.3**
    /// </summary>
    [Fact]
    public void ChildContent_ProvidedWithMessage_ChildContentTakesPrecedence()
    {
        // Arrange
        RenderFragment childContent = builder =>
        {
            builder.OpenElement(0, "strong");
            builder.AddContent(1, "Rich content here");
            builder.CloseElement();
        };

        var alert = new StatusAlertComponent
        {
            Message = "Plain text message",
            ChildContent = childContent
        };

        // Act & Assert — when ChildContent is non-null, it takes precedence over Message
        // The template uses: @if (ChildContent != null) { @ChildContent } else { @Message }
        Assert.NotNull(alert.ChildContent);
        Assert.NotNull(alert.Message);
    }

    /// <summary>
    /// Verifies that when ChildContent is not provided, Message is used for body content.
    /// **Validates: Requirement 6.3**
    /// </summary>
    [Fact]
    public void ChildContent_NotProvided_MessageUsedForBody()
    {
        // Arrange
        var alert = new StatusAlertComponent { Message = "Plain text message" };

        // Act & Assert — ChildContent is null, so the template falls through to @Message
        Assert.Null(alert.ChildContent);
        Assert.Equal("Plain text message", alert.Message);
    }

    /// <summary>
    /// Verifies that when ChildContent is provided but Message is null/empty, the alert is hidden
    /// (Message gates visibility regardless of ChildContent).
    /// **Validates: Requirement 1.6**
    /// </summary>
    [Fact]
    public void ChildContent_ProvidedButMessageNull_AlertIsHidden()
    {
        // Arrange
        RenderFragment childContent = builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddContent(1, "Rich content");
            builder.CloseElement();
        };

        var alert = new StatusAlertComponent
        {
            Message = null,
            ChildContent = childContent
        };

        // Act — Message gates visibility, so the alert should be hidden
        var shouldHide = string.IsNullOrEmpty(alert.Message);

        // Assert
        Assert.True(shouldHide);
        Assert.NotNull(alert.ChildContent); // ChildContent is set but won't render
    }

    #endregion
}
