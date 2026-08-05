using System.Reflection;
using AspireWebAppTemplate.Application.Abstractions;
using AspireWebAppTemplate.ApiService.Controllers;
using AspireWebAppTemplate.Application.Contracts.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AspireWebAppTemplate.Tests.AiIntegration;

/// <summary>
/// Unit tests for <see cref="AiController"/> verifying HTTP-layer behavior only.
/// Mocks <see cref="IAiService"/> to assert correct status codes, exception-to-status mapping,
/// and proper authorization configuration.
/// </summary>
public class AiControllerUnitTests
{
    #region Setup

    private readonly Mock<IAiService> _mockAiService;
    private readonly AiController _controller;

    /// <summary>
    /// Initializes test fixtures with a mocked IAiService and a controller instance.
    /// </summary>
    public AiControllerUnitTests()
    {
        _mockAiService = new Mock<IAiService>();
        _controller = new AiController(_mockAiService.Object);
    }

    #endregion

    #region SendPrompt Success

    /// <summary>
    /// Verifies that a valid prompt request returns 200 OK with the AiResponseDto
    /// produced by the service.
    /// </summary>
    [Fact]
    public async Task SendPrompt_ValidRequest_Returns200WithResponse()
    {
        // Arrange
        var request = new AiPromptRequest { Prompt = "Tell me about .NET" };
        var expectedResponse = new AiResponseDto { GeneratedText = "Here is information about .NET..." };

        _mockAiService
            .Setup(s => s.SendPromptAsync(request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.SendPrompt(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var responseDto = Assert.IsType<AiResponseDto>(okResult.Value);
        Assert.Equal(expectedResponse.GeneratedText, responseDto.GeneratedText);

        _mockAiService.Verify(s => s.SendPromptAsync(request), Times.Once);
    }

    #endregion

    #region Exception Mapping

    /// <summary>
    /// Verifies that an ArgumentException thrown by the service maps to a 400 Bad Request
    /// response with the exception message as the body.
    /// </summary>
    [Fact]
    public async Task SendPrompt_ArgumentException_Returns400()
    {
        // Arrange
        var request = new AiPromptRequest { Prompt = "" };
        var exceptionMessage = "Prompt text is required.";

        _mockAiService
            .Setup(s => s.SendPromptAsync(request))
            .ThrowsAsync(new ArgumentException(exceptionMessage));

        // Act
        var result = await _controller.SendPrompt(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(exceptionMessage, badRequestResult.Value);
    }

    /// <summary>
    /// Verifies that an InvalidOperationException thrown by the service maps to a 400 Bad Request
    /// response with the exception message as the body.
    /// </summary>
    [Fact]
    public async Task SendPrompt_InvalidOperationException_Returns400()
    {
        // Arrange
        var request = new AiPromptRequest { Prompt = "Hello" };
        var exceptionMessage = "The AI service is temporarily unavailable.";

        _mockAiService
            .Setup(s => s.SendPromptAsync(request))
            .ThrowsAsync(new InvalidOperationException(exceptionMessage));

        // Act
        var result = await _controller.SendPrompt(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(exceptionMessage, badRequestResult.Value);
    }

    #endregion

    #region Authorization

    /// <summary>
    /// Verifies that the AiController class is decorated with the [Authorize] attribute,
    /// ensuring all endpoints require authentication.
    /// </summary>
    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        // Act
        var authorizeAttribute = typeof(AiController)
            .GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        Assert.NotNull(authorizeAttribute);
    }

    #endregion
}
