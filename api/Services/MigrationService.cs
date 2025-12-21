using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Probate.Db.Models;

namespace Probate.Api.Services
{
    public class MigrationService
    {
        private readonly ProbateDbContext _dbContext;
        private readonly ILogger<MigrationService> _logger;

        public MigrationService(ProbateDbContext dbContext, ILogger<MigrationService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task ExecuteMigrationsAsync()
        {
            try
            {
                _logger.LogInformation("Applying database migrations...");
                await _dbContext.Database.MigrateAsync();
                _logger.LogInformation("Database migrations applied successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying database migrations");
                throw;
            }
        }
    }
}
