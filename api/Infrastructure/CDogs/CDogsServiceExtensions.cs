using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Probate.Api.Infrastructure.Options;
using Refit;

namespace Probate.Api.Infrastructure.CDogs
{
    public static class CDogsServiceExtensions
    {
        public static IServiceCollection AddCDogsApi(
            this IServiceCollection services,
            IConfiguration configuration
        )
        {
            var options = configuration.GetSection(CDogsOptions.SectionName).Get<CDogsOptions>();

            services.Configure<CDogsOptions>(configuration.GetSection(CDogsOptions.SectionName));

            services.AddTransient<CDogsAuthHandler>();

            services
                .AddRefitClient<ICDogsApi>()
                .ConfigureHttpClient(c => c.BaseAddress = new Uri(options.BaseUrl))
                .AddHttpMessageHandler<CDogsAuthHandler>();

            services.AddScoped<ICDogsDelegate, CDogsDelegate>();

            return services;
        }
    }
}
