using System;
using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Options;
using Probate.Api.Infrastructure.Options;

namespace Probate.Api.Infrastructure.DataProtection
{
    /// <summary>
    /// Encrypts Data Protection key XML with the configured AES-256-GCM key before it is persisted.
    /// </summary>
    public sealed class AesGcmXmlEncryptor : IXmlEncryptor
    {
        private readonly IOptions<DataProtectionOptions> _options;

        public AesGcmXmlEncryptor(IOptions<DataProtectionOptions> options)
        {
            _options = options;
        }

        public EncryptedXmlInfo Encrypt(XElement plaintextElement)
        {
            ArgumentNullException.ThrowIfNull(plaintextElement);

            var key = Convert.FromBase64String(_options.Value.EncryptionKey);
            try
            {
                using var aes = new AesGcmService(key);
                return new EncryptedXmlInfo(
                    new XElement(
                        "encryptedKey",
                        new XElement(
                            "value",
                            aes.Encrypt(plaintextElement.ToString(SaveOptions.DisableFormatting))
                        )
                    ),
                    typeof(AesGcmXmlDecryptor)
                );
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }
}
