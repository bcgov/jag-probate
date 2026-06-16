using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Probate.Api.Helpers;
using Probate.Api.Infrastructure.Authentication;
using Probate.Api.Infrastructure.CDogs;
using Probate.Api.Infrastructure.Chefs;
using Probate.Api.Infrastructure.EFiling;
using Probate.Api.Infrastructure.Options;
using Probate.Api.Services;
using Probate.Db.Models;

namespace Probate.Api
{
    public class Startup
    {
        private IWebHostEnvironment CurrentEnvironment { get; }

        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            Configuration = configuration;
            CurrentEnvironment = env;
        }

        private IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddProblemDetails();

            // Configure forwarded headers so the app sees the external scheme/host
            // from the nginx reverse proxy. Required for OIDC token exchange to
            // construct the correct redirect_uri.
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor
                    | ForwardedHeaders.XForwardedProto
                    | ForwardedHeaders.XForwardedHost;
                // Trust all proxies (nginx runs in the same pod / overlay network)
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });

            services.RegisterOptions(Configuration);

            services.AddScoped<MigrationService>();
            services.AddScoped<IChefsApplicationService, ChefsApplicationService>();
            services.AddScoped<ISubmissionService, SubmissionService>();
            services.AddScoped<ICourtLocationService, CourtLocationService>();
            services.AddScoped<ITemplateService, TemplateService>();
            services.AddSingleton<IFormSchemaService, FormSchemaService>();

            services.AddHttpClient();
            services.AddMemoryCache();

            services.AddChefsApi(Configuration);
            services.AddCDogsApi(Configuration);
            services.AddEFilingApi(Configuration);

            services.AddDbContext<ProbateDbContext>(options =>
            {
                var databaseOptions =
                    Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
                    ?? throw new InvalidOperationException(
                        $"Missing required configuration section '{DatabaseOptions.SectionName}'."
                    );

                options
                    .UseNpgsql(
                        databaseOptions.ConnectionString,
                        npg =>
                        {
                            npg.MigrationsAssembly("db");
                            npg.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
                        }
                    )
                    .UseSnakeCaseNamingConvention();

                if (CurrentEnvironment.IsDevelopment())
                    options.EnableSensitiveDataLogging();
            });

            var corsOptions =
                Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()
                ?? throw new InvalidOperationException(
                    $"Missing required configuration section '{CorsOptions.SectionName}'."
                );

            services.AddCors(options =>
            {
                options.AddPolicy(
                    "ProbateCorsPolicy",
                    builder =>
                        builder
                            .WithOrigins(corsOptions.AllowedOrigins.Split(','))
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials()
                );
            });

            // Add Authentication & Authorization
            services.AddProbateAuthentication(CurrentEnvironment, Configuration);
            services.AddAuthorization();

            services
                .AddControllers()
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.ContractResolver =
                        new CamelCasePropertyNamesContractResolver();
                    options.SerializerSettings.Formatting = Formatting.Indented;
                    options.SerializerSettings.DateFormatHandling =
                        DateFormatHandling.IsoDateFormat;
                    options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
                    options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                    options.SerializerSettings.Converters.Add(new StringEnumConverter());
                });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc(
                    "v1",
                    new OpenApiInfo
                    {
                        Title = "Jag-Probate API",
                        Version = "v1",
                        Description = "Probate Application System API",
                    }
                );

                c.EnableAnnotations();
            });

            services.AddSwaggerGenNewtonsoftSupport();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Must be first so Request.Scheme, Request.Host, etc. reflect the
            // external URL for all downstream middleware (OIDC, cookies, etc.).
            app.UseForwardedHeaders();
            var basePath = Configuration["WEB_BASE_HREF"]?.Trim();

            if (!string.IsNullOrEmpty(basePath))
            {
                // Ensure leading slash
                if (!basePath.StartsWith("/"))
                    basePath = "/" + basePath;

                // Remove trailing slash except for root
                basePath = basePath.TrimEnd('/');

                // Skip root or empty
                if (!string.IsNullOrEmpty(basePath) && basePath != "/")
                {
                    app.UsePathBase(basePath);
                }
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseExceptionHandler();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Jag-Probate API V1");
                c.RoutePrefix = "api/swagger";
            });

            app.UseRouting();
            app.UseCors("ProbateCorsPolicy");
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
