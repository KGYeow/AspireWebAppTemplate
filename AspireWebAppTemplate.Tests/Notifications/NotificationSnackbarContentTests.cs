using System.Reflection;
using AspireWebAppTemplate.UI.Components.Shared;
using AspireWebAppTemplate.UI.Utilities;

namespace AspireWebAppTemplate.Tests.Notifications;

/// <summary>
/// Unit tests for <see cref="NotificationSnackbarContent"/> verifying category icon/color mapping
/// and graceful handling of null/empty parameters.
/// The component is purely presentational — click handling is tested at the caller level.
/// </summary>
public class NotificationSnackbarContentTests
{
    #region Constructor

    /// <summary>
    /// The component instance under test.
    /// </summary>
    private readonly NotificationSnackbarContent _component;

    /// <summary>
    /// Initializes test fixtures with a NotificationSnackbarContent instance.
    /// </summary>
    public NotificationSnackbarContentTests()
    {
        _component = new NotificationSnackbarContent();
    }

    #endregion

    #region Category Icon Mapping

    /// <summary>
    /// Verifies that the "Account" category maps to the security icon.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Fact]
    public void CategoryIcon_ReturnsSecurityIcon_WhenCategoryIsAccount()
    {
        // Arrange
        SetPublicProperty(_component, "Category", "Account");

        // Act
        var result = GetPrivateProperty<string>(_component, "CategoryIcon");

        // Assert
        Assert.Equal("material-symbols-rounded/security", result);
    }

    /// <summary>
    /// Verifies that the "Account" category maps to the error color class.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Fact]
    public void CategoryColorClass_ReturnsError_WhenCategoryIsAccount()
    {
        // Arrange
        SetPublicProperty(_component, "Category", "Account");

        // Act
        var result = GetPrivateProperty<string>(_component, "CategoryColorClass");

        // Assert
        Assert.Equal("mud-error", result);
    }

    /// <summary>
    /// Verifies that the "Activity" category maps to the people icon.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Fact]
    public void CategoryIcon_ReturnsPeopleIcon_WhenCategoryIsActivity()
    {
        // Arrange
        SetPublicProperty(_component, "Category", "Activity");

        // Act
        var result = GetPrivateProperty<string>(_component, "CategoryIcon");

        // Assert
        Assert.Equal("material-symbols-rounded/people", result);
    }

    /// <summary>
    /// Verifies that the "Activity" category maps to the primary color class.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Fact]
    public void CategoryColorClass_ReturnsPrimary_WhenCategoryIsActivity()
    {
        // Arrange
        SetPublicProperty(_component, "Category", "Activity");

        // Act
        var result = GetPrivateProperty<string>(_component, "CategoryColorClass");

        // Assert
        Assert.Equal("mud-primary", result);
    }

    /// <summary>
    /// Verifies that the "System" category maps to the info icon.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Fact]
    public void CategoryIcon_ReturnsInfoIcon_WhenCategoryIsSystem()
    {
        // Arrange
        SetPublicProperty(_component, "Category", "System");

        // Act
        var result = GetPrivateProperty<string>(_component, "CategoryIcon");

        // Assert
        Assert.Equal("material-symbols-rounded/info", result);
    }

    /// <summary>
    /// Verifies that the "System" category maps to the info color class.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Fact]
    public void CategoryColorClass_ReturnsInfo_WhenCategoryIsSystem()
    {
        // Arrange
        SetPublicProperty(_component, "Category", "System");

        // Act
        var result = GetPrivateProperty<string>(_component, "CategoryColorClass");

        // Assert
        Assert.Equal("mud-info", result);
    }

    #endregion

    #region Null/Empty Parameters

    /// <summary>
    /// Verifies that an empty category returns the default notifications icon and empty color class.
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Fact]
    public void CategoryIcon_ReturnsDefaultIcon_WhenCategoryIsEmpty()
    {
        // Arrange
        SetPublicProperty(_component, "Category", "");

        // Act
        var icon = GetPrivateProperty<string>(_component, "CategoryIcon");
        var colorClass = GetPrivateProperty<string>(_component, "CategoryColorClass");

        // Assert
        Assert.Equal("material-symbols-rounded/notifications", icon);
        Assert.Equal("", colorClass);
    }

    /// <summary>
    /// Verifies that a null category returns the default icon and empty color class without throwing.
    /// </summary>
    [Fact]
    public void CategoryIcon_ReturnsDefaultIcon_WhenCategoryIsNull()
    {
        // Arrange
        SetPublicProperty(_component, "Category", null!);

        // Act
        var icon = GetPrivateProperty<string>(_component, "CategoryIcon");
        var colorClass = GetPrivateProperty<string>(_component, "CategoryColorClass");

        // Assert
        Assert.Equal("material-symbols-rounded/notifications", icon);
        Assert.Equal("", colorClass);
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Sets a public parameter property value (used for [Parameter] properties).
    /// </summary>
    private static void SetPublicProperty(object obj, string propertyName, object value)
    {
        var property = obj.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);

        property?.SetValue(obj, value);
    }

    /// <summary>
    /// Gets a private property value via reflection.
    /// </summary>
    private static T GetPrivateProperty<T>(object obj, string propertyName)
    {
        var property = obj.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        return (T)property?.GetValue(obj)!;
    }

    #endregion
}
