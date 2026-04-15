using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Probate.Api.Helpers.Exceptions;
using Probate.Api.Infrastructure.CDogs;
using Probate.Api.Options;
using Refit;

namespace Probate.Api.Infrastructure.Chefs;

/// <summary>
/// Registers CHEFS API client and options. Fails at startup if BaseUrl or ApiKey are missing.
/// Form ID is passed in per request from the controller (no default).
/// </summary>
public static class ChefsServiceCollectionExtensions
{
    public static IServiceCollection AddChefsApi(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var section = configuration.GetSection(ChefsOptions.SectionName);
        services.Configure<ChefsOptions>(section);

        var baseUrl = section["BaseUrl"];
        var apiKey = section["ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ConfigurationException(
                "CHEFS API requires Chefs:BaseUrl (e.g. Chefs__BaseUrl in .env)."
            );
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ConfigurationException(
                "CHEFS API requires Chefs:ApiKey (e.g. Chefs__ApiKey in .env)."
            );

        services.AddTransient<ChefsApiKeyHandler>();
        services
            .AddRefitClient<IChefsApi>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(baseUrl!.TrimEnd('/'));
            })
            .AddHttpMessageHandler<ChefsApiKeyHandler>();

        return services;
    }
}
