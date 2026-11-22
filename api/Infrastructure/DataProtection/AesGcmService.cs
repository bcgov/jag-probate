using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Probate.Api.Infrastructure.DataProtection
{
    /// <summary>
    /// AES-256-GCM encryption using a Base64 envelope of nonce length, nonce, tag length, tag and
    /// ciphertext (adapted from bcgov/jasper and bcgov/unified-scheduling).
    /// </summary>
    public sealed class AesGcmService : IDisposable
    {
        private static readonly int NonceSize = AesGcm.NonceByteSizes.MaxSize;
        private static readonly int TagSize = AesGcm.TagByteSizes.MaxSize;
        private static readonly int HeaderSize = 4 + NonceSize + 4 + TagSize;

        private readonly AesGcm _aes;

        public AesGcmService(byte[] key)
        {
            _aes = new AesGcm(key, TagSize);
        }

        public string Encrypt(string plaintext)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plaintext);
            var encrypted = new byte[HeaderSize + plainBytes.Length];
            BinaryPrimitives.WriteInt32LittleEndian(encrypted.AsSpan(0, 4), NonceSize);
            BinaryPrimitives.WriteInt32LittleEndian(encrypted.AsSpan(4 + NonceSize, 4), TagSize);
            var nonce = encrypted.AsSpan(4, NonceSize);
            var tag = encrypted.AsSpan(4 + NonceSize + 4, TagSize);
            var ciphertext = encrypted.AsSpan(HeaderSize);
            RandomNumberGenerator.Fill(nonce);

            try
            {
                _aes.Encrypt(nonce, plainBytes, ciphertext, tag);
                return Convert.ToBase64String(encrypted);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }

        public string Decrypt(string ciphertext)
        {
            var encrypted = Convert.FromBase64String(ciphertext);
            if (
                encrypted.Length < HeaderSize
                || BinaryPrimitives.ReadInt32LittleEndian(encrypted.AsSpan(0, 4)) != NonceSize
                || BinaryPrimitives.ReadInt32LittleEndian(encrypted.AsSpan(4 + NonceSize, 4))
                    != TagSize
            )
                throw new CryptographicException("Invalid AES-GCM encrypted key envelope.");

            var plaintext = new byte[encrypted.Length - HeaderSize];
            try
            {
                _aes.Decrypt(
                    encrypted.AsSpan(4, NonceSize),
                    encrypted.AsSpan(HeaderSize),
                    encrypted.AsSpan(4 + NonceSize + 4, TagSize),
                    plaintext
                );
                return Encoding.UTF8.GetString(plaintext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }

        public void Dispose() => _aes.Dispose();
    }
}
