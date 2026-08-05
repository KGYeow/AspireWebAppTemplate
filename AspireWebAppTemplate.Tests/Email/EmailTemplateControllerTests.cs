// Feature: email-smtp-integration, Task 11.2: EmailTemplateController unit tests
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.ApiService.Controllers;
using AspireWebAppTemplate.Application.Contracts.Email;
using AspireWebAppTemplate.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AspireWebAppTemplate.Tests.Email;

/// <summary>
/// Unit tests for <see cref="EmailTemplateController"/> verifying HTTP status code mapping,
/// delegation to mocked services, and the absence of create/delete endpoints.
/// </summary>
/// <remarks>
/// **Validates: Requirements 5.1–5.7**
/// </remarks>
public class EmailTemplateControllerTests
{
    #region Constructor

    /// <summary>
    /// Mock for the email template service used by the controller for query, edit, and preview operations.
    /// </summary>
    private readonly Mock<IEmailTemplateService> _mockTemplateService;

    /// <summary>
    /// The controller under test.
    /// </summary>
    private readonly EmailTemplateController _controller;

    /// <summary>
    /// Initializes test fixtures with mocked dependencies and the controller instance.
    /// </summary>
    public EmailTemplateControllerTests()
    {
        _mockTemplateService = new Mock<IEmailTemplateService>();
        _controller = new EmailTemplateController(_mockTemplateService.Object);
    }

    #endregion

    #region GetAll Tests

    /// <summary>
    /// Verifies that GetAll returns 200 OK with the list from the template service.
    /// **Validates: Requirement 5.1**
    /// </summary>
    [Fact]
    public async Task GetAll_ReturnsOkWithTemplateList()
    {
        // Arrange
        var templates = new List<EmailTemplateDto>
        {
            new() { Id = Guid.NewGuid(), DisplayName = "Welcome Email", Category = EmailTemplateCategory.Business },
            new() { Id = Guid.NewGuid(), DisplayName = "Password Reset", Category = EmailTemplateCategory.System }
        };
        _mockTemplateService.Setup(s => s.GetAllAsync()).ReturnsAsync(templates);

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var returnedList = Assert.IsType<List<EmailTemplateDto>>(okResult.Value);
        Assert.Equal(2, returnedList.Count);
        _mockTemplateService.Verify(s => s.GetAllAsync(), Times.Once);
    }

    #endregion

    #region GetById Tests

    /// <summary>
    /// Verifies that GetById returns 200 OK with the template when found.
    /// **Validates: Requirement 5.2**
    /// </summary>
    [Fact]
    public async Task GetById_WhenTemplateExists_ReturnsOkWithTemplate()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var template = new EmailTemplateDto
        {
            Id = templateId,
            DisplayName = "Welcome Email",
            Subject = "Welcome {{UserName}}",
            HtmlBody = "<p>Welcome!</p>",
            Category = EmailTemplateCategory.Business,
            EmailType = EmailType.WelcomeEmail,
            IsActive = true
        };
        _mockTemplateService.Setup(s => s.GetByIdAsync(templateId)).ReturnsAsync(template);

        // Act
        var result = await _controller.GetById(templateId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var returnedTemplate = Assert.IsType<EmailTemplateDto>(okResult.Value);
        Assert.Equal(templateId, returnedTemplate.Id);
        Assert.Equal("Welcome Email", returnedTemplate.DisplayName);
        _mockTemplateService.Verify(s => s.GetByIdAsync(templateId), Times.Once);
    }

    /// <summary>
    /// Verifies that GetById returns 404 Not Found when KeyNotFoundException is thrown.
    /// **Validates: Requirement 5.2**
    /// </summary>
    [Fact]
    public async Task GetById_WhenTemplateNotFound_ReturnsNotFound()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        _mockTemplateService
            .Setup(s => s.GetByIdAsync(templateId))
            .ThrowsAsync(new KeyNotFoundException("Template not found."));

        // Act
        var result = await _controller.GetById(templateId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
        Assert.Equal("Template not found.", notFoundResult.Value);
    }

    #endregion

    #region Update Tests

    /// <summary>
    /// Verifies that Update returns 200 OK with the updated template on success.
    /// **Validates: Requirement 5.3**
    /// </summary>
    [Fact]
    public async Task Update_WhenSuccessful_ReturnsOkWithUpdatedTemplate()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var request = new UpdateEmailTemplateRequest
        {
            DisplayName = "Updated Welcome",
            Subject = "Hello {{UserName}}",
            HtmlBody = "<p>Updated body</p>",
            PlaceholderHints = "UserName",
            IsActive = true
        };
        var updatedTemplate = new EmailTemplateDto
        {
            Id = templateId,
            DisplayName = "Updated Welcome",
            Subject = "Hello {{UserName}}",
            HtmlBody = "<p>Updated body</p>",
            Category = EmailTemplateCategory.Business,
            IsActive = true
        };
        _mockTemplateService.Setup(s => s.UpdateAsync(templateId, request)).ReturnsAsync(updatedTemplate);

        // Act
        var result = await _controller.Update(templateId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var returnedTemplate = Assert.IsType<EmailTemplateDto>(okResult.Value);
        Assert.Equal("Updated Welcome", returnedTemplate.DisplayName);
        _mockTemplateService.Verify(s => s.UpdateAsync(templateId, request), Times.Once);
    }

    /// <summary>
    /// Verifies that Update returns 404 when template is not found (KeyNotFoundException).
    /// **Validates: Requirement 5.3**
    /// </summary>
    [Fact]
    public async Task Update_WhenTemplateNotFound_ReturnsNotFound()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var request = new UpdateEmailTemplateRequest
        {
            DisplayName = "Updated",
            Subject = "Subject",
            HtmlBody = "<p>Body</p>",
            IsActive = true
        };
        _mockTemplateService
            .Setup(s => s.UpdateAsync(templateId, request))
            .ThrowsAsync(new KeyNotFoundException("Template not found."));

        // Act
        var result = await _controller.Update(templateId, request);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
        Assert.Equal("Template not found.", notFoundResult.Value);
    }

    /// <summary>
    /// Verifies that Update returns 400 Bad Request when targeting a system template (InvalidOperationException).
    /// **Validates: Requirement 5.6**
    /// </summary>
    [Fact]
    public async Task Update_WhenSystemTemplate_ReturnsBadRequest()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var request = new UpdateEmailTemplateRequest
        {
            DisplayName = "Updated",
            Subject = "Subject",
            HtmlBody = "<p>Body</p>",
            IsActive = true
        };
        _mockTemplateService
            .Setup(s => s.UpdateAsync(templateId, request))
            .ThrowsAsync(new InvalidOperationException("System templates cannot be modified via the API."));

        // Act
        var result = await _controller.Update(templateId, request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
        Assert.Equal("System templates cannot be modified via the API.", badRequestResult.Value);
    }

    #endregion

    #region Preview Tests

    /// <summary>
    /// Verifies that Preview returns 200 OK with rendered result on success.
    /// **Validates: Requirement 5.4**
    /// </summary>
    [Fact]
    public async Task Preview_WhenSuccessful_ReturnsOkWithRenderedResult()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var request = new PreviewTemplateRequest
        {
            SampleData = new Dictionary<string, string> { { "UserName", "John" } }
        };
        var renderedResult = new RenderedEmailResult
        {
            Subject = "Welcome John",
            HtmlBody = "<p>Hello John!</p>"
        };
        _mockTemplateService
            .Setup(s => s.RenderPreviewAsync(templateId, request.SampleData))
            .ReturnsAsync(renderedResult);

        // Act
        var result = await _controller.Preview(templateId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var returnedResult = Assert.IsType<RenderedEmailResult>(okResult.Value);
        Assert.Equal("Welcome John", returnedResult.Subject);
        Assert.Equal("<p>Hello John!</p>", returnedResult.HtmlBody);
        _mockTemplateService.Verify(s => s.RenderPreviewAsync(templateId, request.SampleData), Times.Once);
    }

    /// <summary>
    /// Verifies that Preview returns 404 when template is not found (KeyNotFoundException).
    /// **Validates: Requirement 5.4**
    /// </summary>
    [Fact]
    public async Task Preview_WhenTemplateNotFound_ReturnsNotFound()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var request = new PreviewTemplateRequest
        {
            SampleData = new Dictionary<string, string> { { "UserName", "John" } }
        };
        _mockTemplateService
            .Setup(s => s.RenderPreviewAsync(templateId, request.SampleData))
            .ThrowsAsync(new KeyNotFoundException("Template not found."));

        // Act
        var result = await _controller.Preview(templateId, request);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
        Assert.Equal("Template not found.", notFoundResult.Value);
    }

    #endregion

    #region No Create/Delete Endpoints Test

    /// <summary>
    /// Verifies that the EmailTemplateController does NOT expose any POST endpoints for creating
    /// templates or DELETE endpoints for removing templates. The template set is fixed by seed data.
    /// **Validates: Requirement 5.7**
    /// </summary>
    [Fact]
    public void Controller_DoesNotExposeCreateOrDeleteEndpoints()
    {
        // Arrange: get all public methods on the controller type
        var controllerType = typeof(EmailTemplateController);
        var publicMethods = controllerType.GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

        // Act: check for HttpPost attributes that indicate create operations
        var httpPostMethods = publicMethods
            .Where(m => m.GetCustomAttributes(typeof(HttpPostAttribute), false).Any())
            .ToList();

        // Filter out legitimate POST endpoints (preview) — only create would be bare POST to root
        var createEndpoints = httpPostMethods
            .Where(m =>
            {
                var postAttr = (HttpPostAttribute)m.GetCustomAttributes(typeof(HttpPostAttribute), false).First();
                // A "create" endpoint would be a POST with no template (root path) or named "Create"
                var isPreview = postAttr.Template?.Contains("preview") == true;
                return !isPreview;
            })
            .ToList();

        // Check for HttpDelete attributes
        var httpDeleteMethods = publicMethods
            .Where(m => m.GetCustomAttributes(typeof(HttpDeleteAttribute), false).Any())
            .ToList();

        // Assert: no create endpoints and no delete endpoints
        Assert.Empty(createEndpoints);
        Assert.Empty(httpDeleteMethods);
    }

    #endregion
}
