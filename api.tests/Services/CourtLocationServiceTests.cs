using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Probate.Api.Infrastructure.EFiling;
using Probate.Api.Infrastructure.Options;
using Probate.Api.Models;
using Probate.Api.Models.EFiling;
using Probate.Api.Services;
using Xunit;

namespace Probate.Api.Tests.Services;

public class CourtLocationServiceTests
{
    private readonly Mock<IEFilingApi> _mockEFilingApi;
    private readonly Mock<ILogger<CourtLocationService>> _mockLogger;
    private readonly IMemoryCache _memoryCache;
    private readonly EFilingOptions _eFilingOptions;
    private readonly CourtLocationService _service;

    public CourtLocationServiceTests()
    {
        _mockEFilingApi = new Mock<IEFilingApi>();
        _mockLogger = new Mock<ILogger<CourtLocationService>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _eFilingOptions = new EFilingOptions
        {
            BaseUrl = "https://efiling-api-test.example.com",
            CourtLevel = CourtLevelEnum.S,
            KeycloakBaseUrl = "https://keycloak-test.example.com",
            KeycloakRealm = "test-realm",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            Enabled = true
        };

        var options = Microsoft.Extensions.Options.Options.Create(_eFilingOptions);
        _service = new CourtLocationService(
            _mockEFilingApi.Object,
            _memoryCache,
            _mockLogger.Object,
            options
        );
    }

    [Fact]
    public async Task GetCourtLocationsAsync_FetchesFromApiWhenNotCached()
    {
        // Arrange
        var eFilingResponse = new EFilingCourtsResponse
        {
            Courts = new List<EFilingCourt>
            {
                new EFilingCourt
                {
                    Id = 1,
                    IdentifierCode = "4871",
                    Name = "Vancouver Law Courts",
                    Code = "VAN",
                    IsSupremeCourt = true,
                    Address = new EFilingAddress
                    {
                        AddressLine1 = "800 Smithe Street",
                        CityName = "Vancouver",
                        ProvinceName = "British Columbia",
                        PostalCode = "V6Z 2E1",
                        CountryName = "Canada"
                    }
                }
            }
        };

        _mockEFilingApi.Setup(x => x.GetCourtsAsync(CourtLevelEnum.S.ToString())).ReturnsAsync(eFilingResponse);

        // Act
        var result = await _service.GetCourtLocationsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Courts);
        Assert.Equal("Vancouver Law Courts", result.Courts[0].Name);
        Assert.Equal("4871", result.Courts[0].IdentifierCode);
        Assert.True(result.Courts[0].IsSupremeCourt);
        _mockEFilingApi.Verify(x => x.GetCourtsAsync(CourtLevelEnum.S.ToString()), Times.Once);
    }

    [Fact]
    public async Task GetCourtLocationsAsync_ReturnsCachedDataWhenAvailable()
    {
        // Arrange
        var eFilingResponse = new EFilingCourtsResponse
        {
            Courts = new List<EFilingCourt>
            {
                new EFilingCourt
                {
                    Id = 1,
                    IdentifierCode = "4871",
                    Name = "Vancouver Law Courts",
                    Code = "VAN",
                    IsSupremeCourt = true,
                    Address = new EFilingAddress
                    {
                        AddressLine1 = "800 Smithe Street",
                        CityName = "Vancouver",
                        ProvinceName = "British Columbia",
                        PostalCode = "V6Z 2E1",
                        CountryName = "Canada"
                    }
                }
            }
        };

        _mockEFilingApi.Setup(x => x.GetCourtsAsync(It.IsAny<string>())).ReturnsAsync(eFilingResponse);

        // Act - First call should fetch from API
        var firstResult = await _service.GetCourtLocationsAsync();

        // Act - Second call should use cache
        var secondResult = await _service.GetCourtLocationsAsync();

        // Assert
        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal(firstResult.Courts.Count, secondResult.Courts.Count);
        Assert.Equal(firstResult.Courts[0].Name, secondResult.Courts[0].Name);

        // API should be called only once (first call)
        _mockEFilingApi.Verify(x => x.GetCourtsAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GetCourtLocationsAsync_TransformsEFilingResponseToCourtLocationModel()
    {
        // Arrange
        var eFilingResponse = new EFilingCourtsResponse
        {
            Courts = new List<EFilingCourt>
            {
                new EFilingCourt
                {
                    Id = 123,
                    IdentifierCode = "TEST123",
                    Name = "Test Court Name",
                    Code = "TST",
                    IsSupremeCourt = true,
                    Address = new EFilingAddress
                    {
                        AddressLine1 = "Line 1",
                        AddressLine2 = "Line 2",
                        AddressLine3 = "Line 3",
                        CityName = "Test City",
                        ProvinceName = "Test Province",
                        PostalCode = "T1S 2T3",
                        CountryName = "Test Country"
                    }
                }
            }
        };

        _mockEFilingApi.Setup(x => x.GetCourtsAsync(It.IsAny<string>())).ReturnsAsync(eFilingResponse);

        // Act
        var result = await _service.GetCourtLocationsAsync();

        // Assert
        Assert.NotNull(result);
        var court = result.Courts[0];
        Assert.Equal(123, court.Id);
        Assert.Equal("TEST123", court.IdentifierCode);
        Assert.Equal("Test Court Name", court.Name);
        Assert.Equal("TST", court.Code);
        Assert.True(court.IsSupremeCourt);

        Assert.NotNull(court.Address);
        Assert.Equal("Line 1", court.Address.AddressLine1);
        Assert.Equal("Line 2", court.Address.AddressLine2);
        Assert.Equal("Line 3", court.Address.AddressLine3);
        Assert.Equal("Test City", court.Address.CityName);
        Assert.Equal("Test Province", court.Address.ProvinceName);
        Assert.Equal("T1S 2T3", court.Address.PostalCode);
        Assert.Equal("Test Country", court.Address.CountryName);
    }

    [Fact]
    public async Task GetCourtLocationsAsync_HandlesMultipleCourts()
    {
        // Arrange
        var eFilingResponse = new EFilingCourtsResponse
        {
            Courts = new List<EFilingCourt>
            {
                new EFilingCourt
                {
                    Id = 1,
                    IdentifierCode = "4871",
                    Name = "Vancouver Law Courts",
                    Code = "VAN",
                    IsSupremeCourt = true,
                    Address = new EFilingAddress
                    {
                        AddressLine1 = "800 Smithe Street",
                        CityName = "Vancouver",
                        ProvinceName = "British Columbia",
                        PostalCode = "V6Z 2E1",
                        CountryName = "Canada"
                    }
                },
                new EFilingCourt
                {
                    Id = 2,
                    IdentifierCode = "3561",
                    Name = "Victoria Law Courts",
                    Code = "VIC",
                    IsSupremeCourt = true,
                    Address = new EFilingAddress
                    {
                        AddressLine1 = "850 Burdett Avenue",
                        CityName = "Victoria",
                        ProvinceName = "British Columbia",
                        PostalCode = "V8W 1B4",
                        CountryName = "Canada"
                    }
                },
                new EFilingCourt
                {
                    Id = 3,
                    IdentifierCode = "4881",
                    Name = "New Westminster Law Courts",
                    Code = "NEW",
                    IsSupremeCourt = false,
                    Address = new EFilingAddress
                    {
                        AddressLine1 = "651 Carnarvon Street",
                        CityName = "New Westminster",
                        ProvinceName = "British Columbia",
                        PostalCode = "V3M 1E2",
                        CountryName = "Canada"
                    }
                }
            }
        };

        _mockEFilingApi.Setup(x => x.GetCourtsAsync(It.IsAny<string>())).ReturnsAsync(eFilingResponse);

        // Act
        var result = await _service.GetCourtLocationsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Courts.Count);
        Assert.Equal("Vancouver Law Courts", result.Courts[0].Name);
        Assert.Equal("Victoria Law Courts", result.Courts[1].Name);
        Assert.Equal("New Westminster Law Courts", result.Courts[2].Name);
    }

    [Fact]
    public async Task GetCourtLocationsAsync_ReturnsEmptyListWhenApiReturnsNoCourts()
    {
        // Arrange
        var eFilingResponse = new EFilingCourtsResponse { Courts = new List<EFilingCourt>() };

        _mockEFilingApi.Setup(x => x.GetCourtsAsync(It.IsAny<string>())).ReturnsAsync(eFilingResponse);

        // Act
        var result = await _service.GetCourtLocationsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Courts);
    }

    [Fact]
    public async Task GetCourtLocationsAsync_UsesCourtLevelFromOptions()
    {
        // Arrange
        var eFilingResponse = new EFilingCourtsResponse { Courts = new List<EFilingCourt>() };

        _mockEFilingApi.Setup(x => x.GetCourtsAsync(It.IsAny<string>())).ReturnsAsync(eFilingResponse);

        // Act
        await _service.GetCourtLocationsAsync();

        // Assert
        _mockEFilingApi.Verify(
            x => x.GetCourtsAsync(CourtLevelEnum.S.ToString()),
            Times.Once,
            "Service should use CourtLevel from options (S for Supreme)"
        );
    }

    [Fact]
    public async Task GetCourtLocationsAsync_HandlesNullAddress()
    {
        // Arrange
        var eFilingResponse = new EFilingCourtsResponse
        {
            Courts = new List<EFilingCourt>
            {
                new EFilingCourt
                {
                    Id = 1,
                    IdentifierCode = "4871",
                    Name = "Test Court",
                    Code = "TST",
                    IsSupremeCourt = true,
                    Address = null
                }
            }
        };

        _mockEFilingApi.Setup(x => x.GetCourtsAsync(It.IsAny<string>())).ReturnsAsync(eFilingResponse);

        // Act
        var result = await _service.GetCourtLocationsAsync();

        // Assert
        Assert.NotNull(result);
        var court = result.Courts[0];
        Assert.Null(court.Address);
    }

    [Fact]
    public async Task GetCourtLocationsAsync_PreservesIsSupremeCourtFlag()
    {
        // Arrange
        var eFilingResponse = new EFilingCourtsResponse
        {
            Courts = new List<EFilingCourt>
            {
                new EFilingCourt
                {
                    Id = 1,
                    IdentifierCode = "S001",
                    Name = "Supreme Court",
                    Code = "SUP",
                    IsSupremeCourt = true,
                    Address = new EFilingAddress { AddressLine1 = "123 Main St", CityName = "City" }
                },
                new EFilingCourt
                {
                    Id = 2,
                    IdentifierCode = "P001",
                    Name = "Provincial Court",
                    Code = "PRV",
                    IsSupremeCourt = false,
                    Address = new EFilingAddress { AddressLine1 = "456 Oak St", CityName = "Town" }
                }
            }
        };

        _mockEFilingApi.Setup(x => x.GetCourtsAsync(It.IsAny<string>())).ReturnsAsync(eFilingResponse);

        // Act
        var result = await _service.GetCourtLocationsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Courts.Count);
        Assert.True(result.Courts[0].IsSupremeCourt);
        Assert.False(result.Courts[1].IsSupremeCourt);
    }
}
