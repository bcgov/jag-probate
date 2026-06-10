using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Probate.Api.Helpers.Exceptions;
using Probate.Api.Infrastructure.EFiling;
using Probate.Api.Infrastructure.Options;
using Probate.Api.Models.EFiling;
using Xunit;

namespace Probate.Api.Tests.Infrastructure;

public class EFilingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEFilingApi_RegistersIEFilingApiInServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();

        // Act
        services.AddEFilingApi(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var eFilingApi = serviceProvider.GetService<IEFilingApi>();
        Assert.NotNull(eFilingApi);
    }

    [Fact]
    public void AddEFilingApi_RegistersEFilingAuthHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();

        // Act
        services.AddEFilingApi(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var handler = serviceProvider.GetService<EFilingAuthHandler>();
        Assert.NotNull(handler);
    }

    [Fact]
    public void AddEFilingApi_ThrowsWhenBaseUrlMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            { "EFiling:BaseUrl", "" }, // Missing
            { "EFiling:KeycloakBaseUrl", "https://keycloak.test" },
            { "EFiling:KeycloakRealm", "test-realm" },
            { "EFiling:ClientId", "test-client" },
            { "EFiling:ClientSecret", "test-secret" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act & Assert
        var exception = Assert.Throws<ConfigurationException>(() =>
            services.AddEFilingApi(configuration)
        );
        Assert.Contains("BaseUrl", exception.Message);
    }

    [Fact]
    public void AddEFilingApi_ThrowsWhenKeycloakBaseUrlMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            { "EFiling:BaseUrl", "https://efiling.test" },
            { "EFiling:KeycloakBaseUrl", "" }, // Missing
            { "EFiling:KeycloakRealm", "test-realm" },
            { "EFiling:ClientId", "test-client" },
            { "EFiling:ClientSecret", "test-secret" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act & Assert
        var exception = Assert.Throws<ConfigurationException>(() =>
            services.AddEFilingApi(configuration)
        );
        Assert.Contains("KeycloakBaseUrl", exception.Message);
    }

    [Fact]
    public void AddEFilingApi_ThrowsWhenKeycloakRealmMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            { "EFiling:BaseUrl", "https://efiling.test" },
            { "EFiling:KeycloakBaseUrl", "https://keycloak.test" },
            { "EFiling:KeycloakRealm", "" }, // Missing
            { "EFiling:ClientId", "test-client" },
            { "EFiling:ClientSecret", "test-secret" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act & Assert
        var exception = Assert.Throws<ConfigurationException>(() =>
            services.AddEFilingApi(configuration)
        );
        Assert.Contains("KeycloakRealm", exception.Message);
    }

    [Fact]
    public void AddEFilingApi_ThrowsWhenClientIdMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            { "EFiling:BaseUrl", "https://efiling.test" },
            { "EFiling:KeycloakBaseUrl", "https://keycloak.test" },
            { "EFiling:KeycloakRealm", "test-realm" },
            { "EFiling:ClientId", "" }, // Missing
            { "EFiling:ClientSecret", "test-secret" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act & Assert
        var exception = Assert.Throws<ConfigurationException>(() =>
            services.AddEFilingApi(configuration)
        );
        Assert.Contains("ClientId", exception.Message);
    }

    [Fact]
    public void AddEFilingApi_ThrowsWhenClientSecretMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            { "EFiling:BaseUrl", "https://efiling.test" },
            { "EFiling:KeycloakBaseUrl", "https://keycloak.test" },
            { "EFiling:KeycloakRealm", "test-realm" },
            { "EFiling:ClientId", "test-client" },
            { "EFiling:ClientSecret", "" } // Missing
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act & Assert
        var exception = Assert.Throws<ConfigurationException>(() =>
            services.AddEFilingApi(configuration)
        );
        Assert.Contains("ClientSecret", exception.Message);
    }

    [Fact]
    public void AddEFilingApi_SucceedsWithValidConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();

        // Act
        services.AddEFilingApi(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - All services should be registered
        Assert.NotNull(serviceProvider.GetService<IEFilingApi>());
        Assert.NotNull(serviceProvider.GetService<EFilingAuthHandler>());
    }

    [Fact]
    public void AddEFilingApi_DefaultsCourtLevelToSupreme()
    {
        // Arrange
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            { "EFiling:BaseUrl", "https://efiling.test" },
            { "EFiling:KeycloakBaseUrl", "https://keycloak.test" },
            { "EFiling:KeycloakRealm", "test-realm" },
            { "EFiling:ClientId", "test-client" },
            { "EFiling:ClientSecret", "test-secret" }
            // Note: CourtLevel not specified
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act
        services.Configure<EFilingOptions>(configuration.GetSection("EFiling"));
        services.AddEFilingApi(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<EFilingOptions>>();
        Assert.NotNull(options);
        Assert.Equal(CourtLevelEnum.S, options!.Value.CourtLevel);
    }

    private static IConfiguration BuildConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            { "EFiling:BaseUrl", "https://efiling-api-test.example.com" },
            { "EFiling:CourtLevel", "S" },
            { "EFiling:KeycloakBaseUrl", "https://keycloak-test.example.com" },
            { "EFiling:KeycloakRealm", "test-realm" },
            { "EFiling:ClientId", "test-client-id" },
            { "EFiling:ClientSecret", "test-client-secret" },
            { "EFiling:Enabled", "true" }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();
    }
}
