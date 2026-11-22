using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Probate.Api.Infrastructure.DataProtection;
using Probate.Db.Models;
using DataProtectionOptions = Probate.Api.Infrastructure.Options.DataProtectionOptions;

namespace Probate.Api.Tests.Infrastructure;

public class DataProtectionTests
{
    private static readonly string KeyA = Convert.ToBase64String(
        RandomNumberGenerator.GetBytes(32)
    );
    private static readonly string KeyB = Convert.ToBase64String(
        RandomNumberGenerator.GetBytes(32)
    );

    [Fact]
    public void Replicas_SharingStoreAndKey_ReadEachOthersPayloads_AndStoreOnlyEncryptedKeys()
    {
        var store = new InMemoryDatabaseRoot();
        var dbName = Guid.NewGuid().ToString();
        using var replica1 = BuildProvider(KeyA, store, dbName);
        using var replica2 = BuildProvider(KeyA, store, dbName);

        var payload = replica1.GetDataProtector("test").Protect("ticket");

        Assert.Equal("ticket", replica2.GetDataProtector("test").Unprotect(payload));
        ProbateDataProtectionExtensions.ValidatePersistedKeys(replica2);

        var stored = ReadStoredKeyXml(replica1);
        Assert.NotEmpty(stored);
        Assert.All(stored, xml =>
        {
            Assert.Contains(nameof(AesGcmXmlDecryptor), xml);
            Assert.DoesNotContain("<masterKey", xml);
        });
    }

    [Fact]
    public void ValidatePersistedKeys_WithWrongEncryptionKey_Throws()
    {
        var store = new InMemoryDatabaseRoot();
        var dbName = Guid.NewGuid().ToString();
        using (var writer = BuildProvider(KeyA, store, dbName))
            writer.GetDataProtector("test").Protect("ticket");

        using var reader = BuildProvider(KeyB, store, dbName);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ProbateDataProtectionExtensions.ValidatePersistedKeys(reader)
        );
        Assert.Contains("DataProtection__EncryptionKey", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA==")] // 16 bytes
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // 36 bytes
    public void AddProbateDataProtection_RejectsInvalidEncryptionKey(string encryptionKey)
    {
        using var provider = BuildProvider(encryptionKey, new InMemoryDatabaseRoot(), "invalid");

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<DataProtectionOptions>>().Value
        );
    }

    [Fact]
    public void AesGcmService_RejectsTamperedCiphertext()
    {
        using var aes = new AesGcmService(Convert.FromBase64String(KeyA));
        var encrypted = Convert.FromBase64String(aes.Encrypt("secret"));
        encrypted[^1] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() =>
            aes.Decrypt(Convert.ToBase64String(encrypted))
        );
    }

    private static ServiceProvider BuildProvider(
        string encryptionKey,
        InMemoryDatabaseRoot store,
        string dbName
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["DataProtection:EncryptionKey"] = encryptionKey }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ProbateDbContext>(options =>
            options.UseInMemoryDatabase(dbName, store)
        );
        services.AddProbateDataProtection(configuration).PersistKeysToDbContext<ProbateDbContext>();
        return services.BuildServiceProvider();
    }

    private static List<string> ReadStoredKeyXml(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return scope
            .ServiceProvider.GetRequiredService<ProbateDbContext>()
            .DataProtectionKeys.Select(key => key.Xml!)
            .ToList();
    }
}
