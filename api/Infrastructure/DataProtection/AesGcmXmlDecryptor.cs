using System;
using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Probate.Api.Infrastructure.Options;

namespace Probate.Api.Infrastructure.DataProtection
{
    /// <summary>
    /// Decrypts persisted Data Protection key XML written by <see cref="AesGcmXmlEncryptor"/>.
    /// </summary>
    /// <remarks>
    /// Data Protection activates this type by name from the stored key XML, so keep its
    /// namespace and name stable or existing keys become unreadable.
    /// </remarks>
    public sealed class AesGcmXmlDecryptor : IXmlDecryptor
    {
        private readonly IServiceProvider _services;

        public AesGcmXmlDecryptor(IServiceProvider services)
        {
            _services = services;
        }

        public XElement Decrypt(XElement encryptedElement)
        {
            ArgumentNullException.ThrowIfNull(encryptedElement);

            var value = encryptedElement.Element("value")?.Value
                ?? throw new CryptographicException("Encrypted Data Protection key has no value.");
            var options = _services.GetRequiredService<IOptions<DataProtectionOptions>>().Value;
            var key = Convert.FromBase64String(options.EncryptionKey);
            try
            {
                using var aes = new AesGcmService(key);
                return XElement.Parse(aes.Decrypt(value));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }
}
