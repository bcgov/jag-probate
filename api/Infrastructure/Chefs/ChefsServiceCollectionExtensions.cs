using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Probate.Api.Helpers.Exceptions;
using Probate.Api.Infrastructure.CDogs;
using Probate.Api.Options;
using Refit;

namespace Probate.Api.Infrastructure.Chefs;

/// <summary>
/// Registers CHEFS API client and options. Fails at startup if BaseUrl or Forms are missing.
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

        ChefsOptions? chefsOptions = section.Exists() ? section.Get<ChefsOptions>() : null;

        var baseUrl = chefsOptions?.BaseUrl;
        var forms = chefsOptions?.Forms;

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ConfigurationException(
                "CHEFS API requires Chefs:BaseUrl (e.g. Chefs__BaseUrl in .env)."
            );
        if (forms == null || forms.Count == 0)
            throw new ConfigurationException(
                "CHEFS API requires Chefs:Forms (e.g. Chefs__Forms in .env)."
            );

        foreach (var (formKey, formOptions) in forms)
        {
            if (string.IsNullOrWhiteSpace(formKey))
                throw new ConfigurationException(
                    "CHEFS API requires each Chefs:Forms entry to have a non-empty logical key."
                );

            if (string.IsNullOrWhiteSpace(formOptions?.FormId))
                throw new ConfigurationException(
                    $"CHEFS API requires env variable Chefs:Forms:{formKey}:FormId (e.g. Chefs__Forms__{formKey}__FormId)."
                );

            if (string.IsNullOrWhiteSpace(formOptions.ApiKey))
                throw new ConfigurationException(
                    $"CHEFS API requires env variable Chefs:Forms:{formKey}:ApiKey (e.g. Chefs__Forms__{formKey}__ApiKey)."
                );
        }

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
