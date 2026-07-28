using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Probate.Api.Helpers.Exceptions;
using Probate.Api.Infrastructure.Chefs;

namespace Probate.Api.Tests.Infrastructure;

public class ChefsServiceCollectionExtensionsTests
{
    private const string LegalFormId = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public void AddChefsApi_RegistersChefsApiWithValidConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();

        services.AddChefsApi(configuration);
        var serviceProvider = services.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetService<IChefsApi>());
        Assert.NotNull(serviceProvider.GetService<ChefsApiKeyHandler>());
    }

    [Fact]
    public void AddChefsApi_ThrowsWhenFormsMissing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?> { ["Chefs:BaseUrl"] = "https://chefs.example.com/app" }
        );

        var exception = Assert.Throws<ConfigurationException>(() =>
            services.AddChefsApi(configuration)
        );

        Assert.Contains("Chefs:Forms", exception.Message);
    }

    [Fact]
    public void AddChefsApi_ThrowsWhenFormKeyBlank()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Chefs:BaseUrl"] = "https://chefs.example.com/app",
                ["Chefs:Forms: :FormId"] = LegalFormId,
                ["Chefs:Forms: :ApiKey"] = "legal-api-key",
            }
        );

        var exception = Assert.Throws<ConfigurationException>(() =>
            services.AddChefsApi(configuration)
        );

        Assert.Contains("non-empty logical key", exception.Message);
    }

    [Fact]
    public void AddChefsApi_ThrowsWhenFormIdMissing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Chefs:BaseUrl"] = "https://chefs.example.com/app",
                ["Chefs:Forms:legal:FormId"] = "",
                ["Chefs:Forms:legal:ApiKey"] = "legal-api-key",
            }
        );

        var exception = Assert.Throws<ConfigurationException>(() =>
            services.AddChefsApi(configuration)
        );

        Assert.Contains("Chefs:Forms:legal:FormId", exception.Message);
        Assert.Contains("Chefs__Forms__legal__FormId", exception.Message);
    }

    [Fact]
    public void AddChefsApi_ThrowsWhenApiKeyMissing()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Chefs:BaseUrl"] = "https://chefs.example.com/app",
                ["Chefs:Forms:legal:FormId"] = LegalFormId,
                ["Chefs:Forms:legal:ApiKey"] = " ",
            }
        );

        var exception = Assert.Throws<ConfigurationException>(() =>
            services.AddChefsApi(configuration)
        );

        Assert.Contains("Chefs:Forms:legal:ApiKey", exception.Message);
        Assert.Contains("Chefs__Forms__legal__ApiKey", exception.Message);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? overrides = null)
    {
        var configData =
            overrides
            ?? new Dictionary<string, string?>
            {
                ["Chefs:BaseUrl"] = "https://chefs.example.com/app",
                ["Chefs:Forms:legal:FormId"] = LegalFormId,
                ["Chefs:Forms:legal:ApiKey"] = "legal-api-key",
            };

        return new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
    }
}
