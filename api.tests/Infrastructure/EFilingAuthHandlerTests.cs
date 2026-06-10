using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Probate.Api.Infrastructure.EFiling;
using Probate.Api.Infrastructure.Options;
using Probate.Api.Models.EFiling;
using Xunit;

namespace Probate.Api.Tests.Infrastructure;

public class EFilingAuthHandlerTests
{
    private readonly Mock<ILogger<EFilingAuthHandler>> _mockLogger;
    private readonly EFilingOptions _eFilingOptions;

    public EFilingAuthHandlerTests()
    {
        _mockLogger = new Mock<ILogger<EFilingAuthHandler>>();
        _eFilingOptions = new EFilingOptions
        {
            BaseUrl = "https://efiling-api-test.example.com",
            CourtLevel = CourtLevelEnum.S,
            KeycloakBaseUrl = "https://keycloak-test.example.com",
            KeycloakRealm = "test-realm",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            Enabled = true
        };
    }

    [Fact]
    public void Constructor_WithValidOptions_CreatesInstance()
    {
        // Arrange & Act
        var options = Microsoft.Extensions.Options.Options.Create(_eFilingOptions);
        var handler = new EFilingAuthHandler(options, _mockLogger.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void EFilingOptions_TokenUrl_ComputedCorrectly()
    {
        // Arrange & Act
        var expectedTokenUrl = $"{_eFilingOptions.KeycloakBaseUrl}/auth/realms/{_eFilingOptions.KeycloakRealm}/protocol/openid-connect/token";

        // Assert
        Assert.Equal(expectedTokenUrl, _eFilingOptions.TokenUrl);
    }

    [Fact]
    public void EFilingOptions_TokenUrl_WithTrailingSlash()
    {
        // Arrange
        var options = new EFilingOptions
        {
            BaseUrl = "https://efiling.test",
            CourtLevel = CourtLevelEnum.S,
            KeycloakBaseUrl = "https://keycloak.test/",  // With trailing slash
            KeycloakRealm = "test-realm",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            Enabled = true
        };

        // Act
        var tokenUrl = options.TokenUrl;

        // Assert
        Assert.DoesNotContain("//auth/realms", tokenUrl);
        Assert.Contains("/auth/realms/test-realm/protocol/openid-connect/token", tokenUrl);
    }

    [Fact]
    public void EFilingOptions_ClientCredentials_StoredCorrectly()
    {
        // Arrange & Act
        var base64Credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_eFilingOptions.ClientId}:{_eFilingOptions.ClientSecret}")
        );

        // Assert
        Assert.NotNull(_eFilingOptions.ClientId);
        Assert.NotNull(_eFilingOptions.ClientSecret);
        Assert.NotEmpty(base64Credentials);
    }

    [Fact]
    public void EFilingOptions_DefaultCourtLevel_IsSupreme()
    {
        // Arrange & Act
        var options = new EFilingOptions
        {
            BaseUrl = "https://test",
            KeycloakBaseUrl = "https://keycloak",
            KeycloakRealm = "realm",
            ClientId = "client",
            ClientSecret = "secret"
        };

        // Assert
        Assert.Equal(CourtLevelEnum.S, options.CourtLevel);
    }

    [Fact]
    public void EFilingOptions_Enabled_DefaultsToTrue()
    {
        // Arrange & Act
        var options = new EFilingOptions
        {
            BaseUrl = "https://test",
            KeycloakBaseUrl = "https://keycloak",
            KeycloakRealm = "realm",
            ClientId = "client",
            ClientSecret = "secret"
        };

        // Assert
        Assert.True(options.Enabled);
    }
}
