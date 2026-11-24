using System.ComponentModel.DataAnnotations;

namespace Probate.Api.Infrastructure.Options
{
    /// <summary>
    /// Configuration options for ASP.NET Core Data Protection key encryption at rest.
    /// These options are bound from the "DataProtection" configuration section.
    /// </summary>
    public sealed class DataProtectionOptions
    {
        public const string SectionName = "DataProtection";

        /// <summary>
        /// Base64 encoding of a random 32-byte (256-bit) AES key. It encrypts the Data Protection
        /// keys stored in the database. Supply it as <c>DataProtection__EncryptionKey</c> from a Secret.
        /// </summary>
        [Required(ErrorMessage = "DataProtection EncryptionKey is required")]
        public string EncryptionKey { get; set; } = string.Empty;
    }
}
