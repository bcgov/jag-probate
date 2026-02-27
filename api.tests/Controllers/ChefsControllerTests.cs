using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Probate.Api.Controllers;
using Probate.Api.Helpers.Exceptions;
using Probate.Api.Services;
using Xunit;

namespace Probate.Api.Tests.Controllers;

public class ChefsControllerTests
{
    private readonly Mock<IChefsApplicationService> _mockService;
    private readonly Mock<ILogger<ChefsController>> _mockLogger;
    private readonly ChefsController _controller;

    public ChefsControllerTests()
    {
        _mockService = new Mock<IChefsApplicationService>();
        _mockLogger = new Mock<ILogger<ChefsController>>();
        _controller = new ChefsController(_mockService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAuthToken_WithValidFormKey_ReturnsOkWithToken()
    {
        // Arrange
        var formKey = "legal";
        var expectedToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        _mockService
            .Setup(x => x.GetAuthTokenAsync(formKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedToken);

        // Act
        var result = await _controller.GetAuthToken(formKey);

        // Assert
        var okResult = Assert.IsType<ActionResult<string>>(result);
        var okObjectResult = Assert.IsType<OkObjectResult>(okResult.Result);
        Assert.Equal(expectedToken, okObjectResult.Value);
    }

    [Fact]
    public async Task GetAuthToken_WithEmptyFormKey_ReturnsBadRequest()
    {
        // Arrange
        var formKey = "";

        // Act
        var result = await _controller.GetAuthToken(formKey);

        // Assert
        var badRequestResult = Assert.IsType<ActionResult<string>>(result);
        var badRequest = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
        Assert.NotNull(badRequest.Value);
        var message = badRequest
            .Value.GetType()
            .GetProperty("message")
            ?.GetValue(badRequest.Value)
            ?.ToString();
        Assert.Equal("formKey is required.", message);
    }

    [Fact]
    public async Task GetAuthToken_WithSpecialCharactersInFormKey_ReturnsBadRequest()
    {
        // Arrange
        var formKey = "legal;DROP TABLE users;--"; // SQL injection attempt

        // Act
        var result = await _controller.GetAuthToken(formKey);

        // Assert
        var badRequestResult = Assert.IsType<ActionResult<string>>(result);
        var badRequest = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task GetAuthToken_WithValidCharacters_ReturnsOk()
    {
        // Arrange
        var formKey = "legal-form_123"; // hyphens and underscores are allowed
        var expectedToken = "test-token";
        _mockService
            .Setup(x => x.GetAuthTokenAsync(formKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedToken);

        // Act
        var result = await _controller.GetAuthToken(formKey);

        // Assert
        var okResult = Assert.IsType<ActionResult<string>>(result);
        var okObjectResult = Assert.IsType<OkObjectResult>(okResult.Result);
        Assert.Equal(expectedToken, okObjectResult.Value);
    }

    [Fact]
    public async Task GetAuthToken_WhenFormKeyNotConfigured_ReturnsBadRequest()
    {
        // Arrange
        var formKey = "nonexistent";
        _mockService
            .Setup(x => x.GetAuthTokenAsync(formKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new InvalidOperationException("Form key 'nonexistent' is not configured.")
            );

        // Act
        var result = await _controller.GetAuthToken(formKey);

        // Assert
        var badRequestResult = Assert.IsType<ActionResult<string>>(result);
        var badRequest = Assert.IsType<BadRequestObjectResult>(badRequestResult.Result);
        var message = badRequest
            .Value.GetType()
            .GetProperty("message")
            ?.GetValue(badRequest.Value)
            ?.ToString();
        Assert.Contains("not configured", message);
    }

    [Fact]
    public async Task GetAuthToken_WhenChefsApiReturns404_ReturnsProblemDetails()
    {
        // Arrange
        var formKey = "legal";
        _mockService
            .Setup(x => x.GetAuthTokenAsync(formKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ChefsApiException("Form not found", HttpStatusCode.NotFound));

        // Act
        var result = await _controller.GetAuthToken(formKey);

        // Assert
        var objectResult = Assert.IsType<ActionResult<string>>(result);
        var problemResult = Assert.IsType<ObjectResult>(objectResult.Result);
        Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
    }

    [Fact]
    public async Task GetAuthToken_WhenChefsApiReturns500_ReturnsProblemDetails()
    {
        // Arrange
        var formKey = "legal";
        _mockService
            .Setup(x => x.GetAuthTokenAsync(formKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new ChefsApiException("Internal server error", HttpStatusCode.InternalServerError)
            );

        // Act
        var result = await _controller.GetAuthToken(formKey);

        // Assert
        var objectResult = Assert.IsType<ActionResult<string>>(result);
        var problemResult = Assert.IsType<ObjectResult>(objectResult.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problemResult.StatusCode);
    }

    [Fact]
    public async Task GetAuthToken_WhenChefsApiUnreachable_Returns502()
    {
        // Arrange
        var formKey = "legal";
        _mockService
            .Setup(x => x.GetAuthTokenAsync(formKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new ChefsApiException("Unable to reach CHEFS API", HttpStatusCode.BadGateway)
            );

        // Act
        var result = await _controller.GetAuthToken(formKey);

        // Assert
        var objectResult = Assert.IsType<ActionResult<string>>(result);
        var problemResult = Assert.IsType<ObjectResult>(objectResult.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, problemResult.StatusCode);
    }

    [Fact]
    public async Task GetAuthToken_VerifiesServiceIsCalledOnce()
    {
        // Arrange
        var formKey = "legal";
        var expectedToken = "test-token";
        _mockService
            .Setup(x => x.GetAuthTokenAsync(formKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedToken);

        // Act
        await _controller.GetAuthToken(formKey);

        // Assert
        _mockService.Verify(
            x => x.GetAuthTokenAsync(formKey, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
