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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyAllConfigurations();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
                optionsBuilder.UseNpgsql("Name=DatabaseConnectionString");
        }
    }
}
