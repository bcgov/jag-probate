using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Probate.Api.Helpers.Exceptions;
using Probate.Api.Infrastructure.Chefs;
using Probate.Api.Options;
using Probate.Api.Services;
using Refit;
using Xunit;

namespace Probate.Api.Tests.Services;

public class ChefsApplicationServiceTests
{
    private readonly Mock<IChefsApi> _mockChefsApi;
    private readonly Mock<ILogger<ChefsApplicationService>> _mockLogger;
    private readonly ChefsOptions _chefsOptions;
    private readonly ChefsApplicationService _service;

    public ChefsApplicationServiceTests()
    {
        _mockChefsApi = new Mock<IChefsApi>();
        _mockLogger = new Mock<ILogger<ChefsApplicationService>>();
        _chefsOptions = new ChefsOptions
        {
            Forms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "legal", "12345678-1234-1234-1234-123456789012" },
                { "probate", "87654321-4321-4321-4321-210987654321" },
            },
        };

        var options = Microsoft.Extensions.Options.Options.Create(_chefsOptions);
        _service = new ChefsApplicationService(_mockChefsApi.Object, options, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAuthTokenAsync_WithValidFormKey_ReturnsToken()
    {
        // Arrange
        var formKey = "legal";
        var expectedToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test.token";
        var response = new ChefsAuthTokenResponse { Token = expectedToken };

        _mockChefsApi
            .Setup(x =>
                x.GetAuthTokenAsync(
                    It.IsAny<string>(),
                    It.IsAny<ChefsAuthTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(response);

        // Act
        var result = await _service.GetAuthTokenAsync(formKey);

        // Assert
        Assert.Equal(expectedToken, result);
        _mockChefsApi.Verify(
            x =>
                x.GetAuthTokenAsync(
                    "12345678-1234-1234-1234-123456789012",
                    It.IsAny<ChefsAuthTokenRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetAuthTokenAsync_WithInvalidFormKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var formKey = "nonexistent";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetAuthTokenAsync(formKey)
        );
        Assert.Contains("not configured", exception.Message);
    }

    [Fact]
    public async Task GetAuthTokenAsync_WithEmptyFormKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var formKey = "";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetAuthTokenAsync(formKey)
        );
        Assert.Contains("not configured", exception.Message);
    }

    [Fact]
    public async Task GetAuthTokenAsync_WhenChefsApiReturns404_ThrowsChefsApiException()
    {
        // Arrange
        var formKey = "legal";
        var apiException = await ApiException.Create(
            new HttpRequestMessage(),
            HttpMethod.Post,
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("Form not found"),
            },
            new RefitSettings()
        );

        _mockChefsApi
            .Setup(x =>
                x.GetAuthTokenAsync(
                    It.IsAny<string>(),
                    It.IsAny<ChefsAuthTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(apiException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ChefsApiException>(() =>
            _service.GetAuthTokenAsync(formKey)
        );
        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Contains("Form not found", exception.Message);
    }

    [Fact]
    public async Task GetAuthTokenAsync_WhenChefsApiReturns401_ThrowsChefsApiException()
    {
        // Arrange
        var formKey = "legal";
        var apiException = await ApiException.Create(
            new HttpRequestMessage(),
            HttpMethod.Post,
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("Unauthorized"),
            },
            new RefitSettings()
        );

        _mockChefsApi
            .Setup(x =>
                x.GetAuthTokenAsync(
                    It.IsAny<string>(),
                    It.IsAny<ChefsAuthTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(apiException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ChefsApiException>(() =>
            _service.GetAuthTokenAsync(formKey)
        );
        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [Fact]
    public async Task GetAuthTokenAsync_WhenHttpRequestFails_ThrowsChefsApiException()
    {
        // Arrange
        var formKey = "legal";
        _mockChefsApi
            .Setup(x =>
                x.GetAuthTokenAsync(
                    It.IsAny<string>(),
                    It.IsAny<ChefsAuthTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ChefsApiException>(() =>
            _service.GetAuthTokenAsync(formKey)
        );
        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.Contains("Unable to reach CHEFS API", exception.Message);
    }

    [Fact]
    public async Task GetAuthTokenAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var formKey = "legal";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockChefsApi
            .Setup(x =>
                x.GetAuthTokenAsync(
                    It.IsAny<string>(),
                    It.IsAny<ChefsAuthTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new TaskCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.GetAuthTokenAsync(formKey, cts.Token)
        );
    }

    [Fact]
    public async Task GetAuthTokenAsync_CaseInsensitiveFormKey_ReturnsToken()
    {
        // Arrange
        var formKey = "LEGAL"; // uppercase
        var expectedToken = "test-token";
        var response = new ChefsAuthTokenResponse { Token = expectedToken };

        _mockChefsApi
            .Setup(x =>
                x.GetAuthTokenAsync(
                    It.IsAny<string>(),
                    It.IsAny<ChefsAuthTokenRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(response);

        // Act
        var result = await _service.GetAuthTokenAsync(formKey);

        // Assert
        Assert.Equal(expectedToken, result);
    }
}
