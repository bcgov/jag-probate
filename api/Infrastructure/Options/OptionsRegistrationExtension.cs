using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Probate.Api.Infrastructure.Options
{
    /// <summary>
    /// Extension methods for registering and configuring application options.
    /// </summary>
    public static class OptionsRegistrationExtension
    {
        /// <summary>
        /// Registers all application configuration options with the service collection.
        /// </summary>
        /// <param name="services">The service collection to add options to</param>
        /// <param name="configuration">The application configuration</param>
        /// <returns>The service collection for fluent chaining</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if configuration validation fails during binding
        /// </exception>
        public static IServiceCollection RegisterOptions(
            this IServiceCollection services,
            IConfiguration configuration
        )
        {
            // Register Keycloak configuration
            services
                .AddOptions<KeycloakOptions>()
                .Bind(configuration.GetSection(KeycloakOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Register Database configuration
            services
                .AddOptions<DatabaseOptions>()
                .Bind(configuration.GetSection(DatabaseOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Register CORS configuration
            services
                .AddOptions<CorsOptions>()
                .Bind(configuration.GetSection(CorsOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Register eFiling configuration
            services
                .AddOptions<EFilingOptions>()
                .Bind(configuration.GetSection(EFilingOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            return services;
        }
    }
}
