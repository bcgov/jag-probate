using System.ComponentModel.DataAnnotations;

namespace Probate.Api.Infrastructure.Options
{
    /// <summary>
    /// Configuration options for database connectivity.
    /// </summary>
    public sealed class DatabaseOptions
    {
        public const string SectionName = "Database";

        /// <summary>
        /// PostgreSQL database connection string.
        /// Should include all necessary connection parameters for the database.
        /// </summary>
        [Required(ErrorMessage = "Database connection string is required")]
        public string ConnectionString { get; set; } = default!;
    }
}
