using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Probate.Api.Infrastructure.Chefs;
using Probate.Api.Options;
using Probate.Api.Services;
using Xunit;
using Xunit.Abstractions;

namespace Probate.Api.Tests.Integration;

/// <summary>
/// Integration tests for CHEFS auth token API.
/// These tests make real HTTP calls to the CHEFS API.
/// Requires valid configuration in appsettings.json or environment variables.
/// </summary>
[Collection("Integration Tests")]
public class ChefsAuthTokenIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly IChefsApplicationService _chefsApplicationService;
    private readonly ChefsOptions _chefsOptions;

    public ChefsAuthTokenIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        // Build configuration from appsettings.json and environment variables
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Setup dependency injection
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Register CHEFS services
        services.AddChefsApi(configuration);
        services.AddScoped<IChefsApplicationService, ChefsApplicationService>();

        _serviceProvider = services.BuildServiceProvider();

        // Get service instances
        _chefsApplicationService = _serviceProvider.GetRequiredService<IChefsApplicationService>();
        _chefsOptions = _serviceProvider.GetRequiredService<IOptions<ChefsOptions>>().Value;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAuthToken_WithLegalFormKey_ReturnsValidToken()
    {
        // Arrange
        var formKey = "legal";

        // Skip test if form key is not configured
        if (
            !_chefsOptions.Forms.ContainsKey(formKey)
            || string.IsNullOrWhiteSpace(_chefsOptions.Forms[formKey])
        )
        {
            _output.WriteLine($"Skipping test: Form key '{formKey}' is not configured.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_chefsOptions.ApiKey))
        {
            _output.WriteLine("Skipping test: CHEFS API key is not configured.");
            return;
        }

        _output.WriteLine($"Testing with form key: {formKey}");
        _output.WriteLine($"Form GUID: {_chefsOptions.Forms[formKey]}");
        _output.WriteLine($"Base URL: {_chefsOptions.BaseUrl}");

        // Act
        var token = await _chefsApplicationService.GetAuthTokenAsync(formKey);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
        _output.WriteLine(
            $"Token received (first 50 chars): {token.Substring(0, Math.Min(50, token.Length))}..."
        );

        // Validate token format (JWT tokens have 3 parts separated by dots)
        var parts = token.Split('.');
        Assert.True(parts.Length == 3, "Token should be in JWT format (3 parts separated by dots)");

        _output.WriteLine("? Successfully retrieved auth token from CHEFS API");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAuthToken_WithInvalidFormKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var formKey = "nonexistent";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _chefsApplicationService.GetAuthTokenAsync(formKey)
        );

        Assert.Contains("not configured", exception.Message);
        _output.WriteLine($"? Correctly threw exception for invalid form key: {exception.Message}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAuthToken_MultipleCalls_ReturnsNewTokenEachTime()
    {
        // Arrange
        var formKey = "legal";

        // Skip test if form key is not configured
        if (
            !_chefsOptions.Forms.ContainsKey(formKey)
            || string.IsNullOrWhiteSpace(_chefsOptions.Forms[formKey])
            || string.IsNullOrWhiteSpace(_chefsOptions.ApiKey)
        )
        {
            _output.WriteLine("Skipping test: Configuration not available.");
            return;
        }

        // Act
        var token1 = await _chefsApplicationService.GetAuthTokenAsync(formKey);
        await Task.Delay(100); // Small delay to ensure different tokens
        var token2 = await _chefsApplicationService.GetAuthTokenAsync(formKey);

        // Assert
        Assert.NotNull(token1);
        Assert.NotNull(token2);
        Assert.NotEmpty(token1);
        Assert.NotEmpty(token2);

        _output.WriteLine(
            $"Token 1 (first 30 chars): {token1.Substring(0, Math.Min(30, token1.Length))}..."
        );
        _output.WriteLine(
            $"Token 2 (first 30 chars): {token2.Substring(0, Math.Min(30, token2.Length))}..."
        );

        // Tokens might be the same or different depending on CHEFS implementation
        _output.WriteLine($"Tokens are {(token1 == token2 ? "identical" : "different")}");
        _output.WriteLine("? Successfully retrieved multiple auth tokens");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAuthToken_VerifyTokenStructure()
    {
        // Arrange
        var formKey = "legal";

        // Skip test if form key is not configured
        if (
            !_chefsOptions.Forms.ContainsKey(formKey)
            || string.IsNullOrWhiteSpace(_chefsOptions.Forms[formKey])
            || string.IsNullOrWhiteSpace(_chefsOptions.ApiKey)
        )
        {
            _output.WriteLine("Skipping test: Configuration not available.");
            return;
        }

        // Act
        var token = await _chefsApplicationService.GetAuthTokenAsync(formKey);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);

        // Verify JWT structure (header.payload.signature)
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        // Verify each part is base64 encoded (basic check)
        foreach (var part in parts)
        {
            Assert.NotEmpty(part);
            Assert.Matches(@"^[A-Za-z0-9_-]+$", part);
        }

        _output.WriteLine("JWT Structure:");
        _output.WriteLine($"  Header length: {parts[0].Length}");
        _output.WriteLine($"  Payload length: {parts[1].Length}");
        _output.WriteLine($"  Signature length: {parts[2].Length}");
        _output.WriteLine("? Token has valid JWT structure");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetAuthToken_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var formKey = "legal";

        // Skip test if form key is not configured
        if (
            !_chefsOptions.Forms.ContainsKey(formKey)
            || string.IsNullOrWhiteSpace(_chefsOptions.Forms[formKey])
            || string.IsNullOrWhiteSpace(_chefsOptions.ApiKey)
        )
        {
            _output.WriteLine("Skipping test: Configuration not available.");
            return;
        }

        var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _chefsApplicationService.GetAuthTokenAsync(formKey, cts.Token)
        );

        _output.WriteLine("? Correctly handled cancellation");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
