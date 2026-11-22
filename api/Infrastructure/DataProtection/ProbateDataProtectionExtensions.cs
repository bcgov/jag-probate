using System;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using DataProtectionOptions = Probate.Api.Infrastructure.Options.DataProtectionOptions;

namespace Probate.Api.Infrastructure.DataProtection
{
    public static class ProbateDataProtectionExtensions
    {
        public const string ApplicationName = "probate";

        // Data Protection marks secret elements that are stored unencrypted with this attribute.
        private static readonly XName RequiresEncryptionAttribute = XName.Get(
            "requiresEncryption",
            "http://schemas.asp.net/2015/03/dataProtection"
        );

        /// <summary>
        /// Registers Data Protection with a stable application name and AES-256-GCM encryption of
        /// keys at rest. The caller selects the key repository (for example <c>PersistKeysToDbContext</c>).
        /// </summary>
        public static IDataProtectionBuilder AddProbateDataProtection(
            this IServiceCollection services,
            IConfiguration configuration
        )
        {
            services
                .AddOptions<DataProtectionOptions>()
                .Bind(configuration.GetSection(DataProtectionOptions.SectionName))
                .ValidateDataAnnotations()
                .Validate(
                    options => IsValidEncryptionKey(options.EncryptionKey),
                    "DataProtection EncryptionKey must be the Base64 encoding of exactly 32 bytes (256 bits)."
                )
                .ValidateOnStart();

            var builder = services.AddDataProtection().SetApplicationName(ApplicationName);
            services.AddSingleton<AesGcmXmlEncryptor>();
            services
                .AddOptions<KeyManagementOptions>()
                .Configure<AesGcmXmlEncryptor>(
                    (options, encryptor) => options.XmlEncryptor = encryptor
                );

            return builder;
        }

        /// <summary>
        /// Fails when any persisted key, including expired keys, cannot be read with the configured
        /// encryption key. Without this check Data Protection would silently create a replacement key
        /// and existing sessions would break across replicas.
        /// </summary>
        public static void ValidatePersistedKeys(IServiceProvider services)
        {
            // A plaintext-only key ring never resolves the options, so validate them explicitly
            // before anything (such as revocation) is persisted.
            _ = services.GetRequiredService<IOptions<DataProtectionOptions>>().Value;

            foreach (var key in services.GetRequiredService<IKeyManager>().GetAllKeys())
            {
                try
                {
                    _ = key.CreateEncryptor()
                        ?? throw new InvalidOperationException(
                            $"Data Protection key {key.KeyId} could not be initialized."
                        );
                }
                catch (Exception ex) when (ex is not InvalidOperationException)
                {
                    throw new InvalidOperationException(
                        $"Data Protection key {key.KeyId} could not be decrypted. Check DataProtection__EncryptionKey.",
                        ex
                    );
                }
            }
        }

        /// <summary>
        /// Revokes persisted keys whose secret is still stored unencrypted (written before key
        /// encryption was enabled). Expired keys still unprotect payloads, so plaintext keys must be
        /// revoked to stop cookie forgery from a database leak. Users holding cookies protected by a
        /// revoked key must sign in again; Data Protection then creates a new, encrypted key.
        /// </summary>
        /// <returns>The number of keys revoked.</returns>
        public static int RevokeUnencryptedKeys(IServiceProvider services)
        {
            _ = services.GetRequiredService<IOptions<DataProtectionOptions>>().Value;

            var repository = services
                .GetRequiredService<IOptions<KeyManagementOptions>>()
                .Value.XmlRepository;
            if (repository is null)
                return 0;

            var unencryptedKeyIds = repository
                .GetAllElements()
                .Where(element => element.Name.LocalName == "key")
                .Where(element =>
                    element
                        .Descendants()
                        .Any(descendant =>
                            (string?)descendant.Attribute(RequiresEncryptionAttribute) == "true"
                        )
                )
                .Select(element => (string?)element.Attribute("id"))
                .Where(id => Guid.TryParse(id, out _))
                .Select(id => Guid.Parse(id!))
                .ToHashSet();
            if (unencryptedKeyIds.Count == 0)
                return 0;

            var keyManager = services.GetRequiredService<IKeyManager>();
            var revoked = 0;
            foreach (var key in keyManager.GetAllKeys())
            {
                if (key.IsRevoked || !unencryptedKeyIds.Contains(key.KeyId))
                    continue;

                keyManager.RevokeKey(
                    key.KeyId,
                    "Stored unencrypted before key encryption was enabled."
                );
                revoked++;
            }

            return revoked;
        }

        private static bool IsValidEncryptionKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            Span<byte> decoded = stackalloc byte[32];
            try
            {
                return Convert.TryFromBase64String(value, decoded, out var written)
                    && written == 32;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(decoded);
            }
        }
    }
}
