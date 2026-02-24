using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Probate.Api.Infrastructure.Authentication;
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

            services.RegisterOptions(Configuration);

            services.AddScoped<MigrationService>();

            services.AddHttpClient();

            services.AddDbContext<ProbateDbContext>(options =>
            {
                var databaseOptions = Configuration
                    .GetSection(DatabaseOptions.SectionName)
                    .Get<DatabaseOptions>();

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

            var corsOptions = Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>();

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
