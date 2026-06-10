using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Probate.Api.Helpers.Exceptions;
using Probate.Api.Infrastructure.Options;
using Refit;

namespace Probate.Api.Infrastructure.EFiling;

/// <summary>
/// Registers eFiling Hub API client and options. Fails at startup if required configuration is missing.
/// </summary>
public static class EFilingServiceCollectionExtensions
{
    public static IServiceCollection AddEFilingApi(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var section = configuration.GetSection(EFilingOptions.SectionName);
        services.Configure<EFilingOptions>(section);

        var baseUrl = section["BaseUrl"];
        var keycloakBaseUrl = section["KeycloakBaseUrl"];
        var keycloakRealm = section["KeycloakRealm"];
        var clientId = section["ClientId"];
        var clientSecret = section["ClientSecret"];

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ConfigurationException("eFiling API requires EFiling:BaseUrl");
        if (string.IsNullOrWhiteSpace(keycloakBaseUrl))
            throw new ConfigurationException("eFiling API requires EFiling:KeycloakBaseUrl");
        if (string.IsNullOrWhiteSpace(keycloakRealm))
            throw new ConfigurationException("eFiling API requires EFiling:KeycloakRealm");
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ConfigurationException("eFiling API requires EFiling:ClientId");
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new ConfigurationException("eFiling API requires EFiling:ClientSecret");

        services.AddTransient<EFilingAuthHandler>();

        var refitSettings = new RefitSettings
        {
            ContentSerializer = new NewtonsoftJsonContentSerializer(
                new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                }
            ),
        };

        services
            .AddRefitClient<IEFilingApi>(refitSettings)
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(baseUrl!.TrimEnd('/'));
            })
            .AddHttpMessageHandler<EFilingAuthHandler>();

        return services;
    }
}
