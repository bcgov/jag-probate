using System;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Probate.Db.Models
{
    public class ProbateDbContext : DbContext, IDataProtectionKeyContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ProbateDbContext() { }

        public ProbateDbContext(
            DbContextOptions<ProbateDbContext> options,
            IHttpContextAccessor httpContextAccessor = null
        )
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // This maps to the table that stores keys.
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
        public DbSet<Case> Cases { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Submission> Submissions { get; set; }
        public DbSet<StepData> StepData { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyAllConfigurations();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var host = Environment.GetEnvironmentVariable("DB_HOST");
                var port = Environment.GetEnvironmentVariable("DB_PORT");
                var username = Environment.GetEnvironmentVariable("DB_USERNAME");
                var password = Environment.GetEnvironmentVariable("DB_PASSWORD");
                var database = Environment.GetEnvironmentVariable("DB_NAME");

                optionsBuilder.UseNpgsql(
                    $"Host={host};Port={port};Username={username};Password={password};Database={database};"
                );
            }
        }
    }
}
