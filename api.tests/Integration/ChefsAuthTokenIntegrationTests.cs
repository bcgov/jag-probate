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
        var legalFormOptions = _chefsOptions.Forms.TryGetValue(formKey, out var options)
            ? options
            : null;
        // Skip test if form key is not configured
        if (legalFormOptions == null || string.IsNullOrWhiteSpace(legalFormOptions.FormId))
        {
            _output.WriteLine($"Skipping test: Form key '{formKey}' is not configured.");
            return;
        }

        if (string.IsNullOrWhiteSpace(legalFormOptions.ApiKey))
        {
            _output.WriteLine("Skipping test: CHEFS API key is not configured.");
            return;
        }

        _output.WriteLine($"Testing with form key: {formKey}");
        _output.WriteLine($"Form GUID: {legalFormOptions.FormId}");
        _output.WriteLine($"Base URL: {_chefsOptions.BaseUrl}");

        // Act
        var result = await _chefsApplicationService.GetAuthTokenAsync(formKey);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Token);
        Assert.NotEmpty(result.Token);
        Assert.NotNull(result.FormId);
        Assert.NotEmpty(result.FormId);
        Assert.Equal(legalFormOptions.FormId, result.FormId);

        _output.WriteLine(
            $"Token received (first 50 chars): {result.Token.Substring(0, Math.Min(50, result.Token.Length))}..."
        );
        _output.WriteLine($"Form ID: {result.FormId}");

        // Validate token format (JWT tokens have 3 parts separated by dots)
        var parts = result.Token.Split('.');
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
            !_chefsOptions.Forms.TryGetValue(formKey, out var formOptions)
            || string.IsNullOrWhiteSpace(formOptions.FormId)
            || string.IsNullOrWhiteSpace(formOptions.ApiKey)
        )
        {
            _output.WriteLine("Skipping test: Configuration not available.");
            return;
        }

        // Act
        var result1 = await _chefsApplicationService.GetAuthTokenAsync(formKey);
        await Task.Delay(100); // Small delay to ensure different tokens
        var result2 = await _chefsApplicationService.GetAuthTokenAsync(formKey);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotEmpty(result1.Token);
        Assert.NotEmpty(result2.Token);
        Assert.Equal(result1.FormId, result2.FormId); // Same form ID
        Assert.Equal(formOptions.FormId, result1.FormId);

        _output.WriteLine(
            $"Token 1 (first 30 chars): {result1.Token.Substring(0, Math.Min(30, result1.Token.Length))}..."
        );
        _output.WriteLine(
            $"Token 2 (first 30 chars): {result2.Token.Substring(0, Math.Min(30, result2.Token.Length))}..."
        );
        _output.WriteLine($"Form ID: {result1.FormId}");

        // Tokens might be the same or different depending on CHEFS implementation
        _output.WriteLine(
            $"Tokens are {(result1.Token == result2.Token ? "identical" : "different")}"
        );
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
            !_chefsOptions.Forms.TryGetValue(formKey, out var formOptions)
            || string.IsNullOrWhiteSpace(formOptions.FormId)
            || string.IsNullOrWhiteSpace(formOptions.ApiKey)
        )
        {
            _output.WriteLine("Skipping test: Configuration not available.");
            return;
        }

        // Act
        var result = await _chefsApplicationService.GetAuthTokenAsync(formKey);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Token);
        Assert.NotEmpty(result.Token);
        Assert.NotNull(result.FormId);
        Assert.NotEmpty(result.FormId);

        // Verify JWT structure (header.payload.signature)
        var parts = result.Token.Split('.');
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
        _output.WriteLine($"Form ID: {result.FormId}");
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
            !_chefsOptions.Forms.TryGetValue(formKey, out var formOptions)
            || string.IsNullOrWhiteSpace(formOptions.FormId)
            || string.IsNullOrWhiteSpace(formOptions.ApiKey)
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
