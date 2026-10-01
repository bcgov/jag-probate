using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Probate.Api.Infrastructure.Options;
using Probate.Api.Services;
using Refit;

namespace Probate.Api.Infrastructure.WeasyPrint;

public static class WeasyPrintServiceCollectionExtensions
{
    public static IServiceCollection AddWeasyPrint(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var options =
            configuration.GetSection(WeasyPrintOptions.SectionName).Get<WeasyPrintOptions>()
            ?? throw new InvalidOperationException("Missing required WeasyPrint configuration.");

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUrl))
            throw new InvalidOperationException("WeasyPrint:BaseUrl must be an absolute URL.");

        services.Configure<WeasyPrintOptions>(
            configuration.GetSection(WeasyPrintOptions.SectionName)
        );
        services
            .AddRefitClient<IWeasyPrintApi>()
            .ConfigureHttpClient(client => client.BaseAddress = baseUrl);
        services.AddScoped<IPDFGenerationService, WeasyPrintPDFService>();
        return services;
    }
}
