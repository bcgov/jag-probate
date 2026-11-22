using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DataProtectionOptions = Probate.Api.Infrastructure.Options.DataProtectionOptions;

namespace Probate.Api.Infrastructure.DataProtection
{
    public static class ProbateDataProtectionExtensions
    {
        public const string ApplicationName = "probate";

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
