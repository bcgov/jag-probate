using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Probate.Api.Services;

namespace Probate.Api
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            var logger = host.Services.GetRequiredService<ILogger<object>>();

            try
            {
                logger.LogInformation(
                    "Application starting in {Environment} environment",
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                );

                await RunMigrationsAsync(host, logger);

                logger.LogInformation("Starting web host");
                await host.RunAsync();
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Application terminated unexpectedly");
                throw new InvalidOperationException(
                    "Application terminated unexpectedly. See inner exception for details.",
                    ex
                );
            }
        }

        private static async Task RunMigrationsAsync(IHost host, ILogger logger)
        {
            logger.LogInformation("Starting database migrations");

            using var scope = host.Services.CreateScope();
            var migrationService = scope.ServiceProvider.GetRequiredService<MigrationService>();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            try
            {
                await migrationService.ExecuteMigrationsAsync().WaitAsync(cts.Token);

                logger.LogInformation("Database migrations completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database migration failed");
                throw;
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
