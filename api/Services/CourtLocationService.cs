using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Probate.Api.Infrastructure.EFiling;
using Probate.Api.Infrastructure.Options;
using Probate.Api.Models;

namespace Probate.Api.Services;

/// <summary>
/// Service for retrieving court location information from eFiling Hub with caching
/// Follows the same pattern as representation-grant-app
/// </summary>
public class CourtLocationService : ICourtLocationService
{
    private const string CacheKey = "CourtLocations";
    private readonly IEFilingApi _eFilingApi;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CourtLocationService> _logger;
    private readonly EFilingOptions _options;

    public CourtLocationService(
        IEFilingApi eFilingApi,
        IMemoryCache cache,
        ILogger<CourtLocationService> logger,
        IOptions<EFilingOptions> options
    )
    {
        _eFilingApi = eFilingApi;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Gets court locations from cache or eFiling Hub API
    /// Cache duration: 24 hours (court locations rarely change)
    /// </summary>
    public async Task<CourtLocationResult> GetCourtLocationsAsync()
    {
        // Try to get from cache first
        if (
            _cache.TryGetValue(CacheKey, out CourtLocationResult? cachedLocations)
            && cachedLocations != null
        )
        {
            _logger.LogDebug("Returning court locations from cache");
            return cachedLocations;
        }

        // Cache miss - fetch from eFiling Hub API
        _logger.LogInformation("Cache miss for court locations. Fetching from eFiling Hub API...");

        var response = await _eFilingApi.GetCourtsAsync(_options.CourtLevel.ToString());

        if (response?.Courts == null || !response.Courts.Any())
        {
            _logger.LogWarning("eFiling Hub API returned no court locations");
            return new CourtLocationResult();
        }

        // Transform eFiling response to our model
        var locations = response
            .Courts.Select(court => new CourtLocationModel
            {
                Id = court.Id,
                IdentifierCode = court.IdentifierCode,
                Name = court.Name,
                Code = court.Code,
                IsSupremeCourt = court.IsSupremeCourt,
                Address =
                    court.Address == null
                        ? null
                        : new CourtAddress
                        {
                            AddressLine1 = court.Address.AddressLine1,
                            AddressLine2 = court.Address.AddressLine2,
                            AddressLine3 = court.Address.AddressLine3,
                            PostalCode = court.Address.PostalCode,
                            CityName = court.Address.CityName,
                            ProvinceName = court.Address.ProvinceName,
                            CountryName = court.Address.CountryName,
                        },
            })
            .ToList();

        var result = new CourtLocationResult { Courts = locations };

        // Cache for 24 hours
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
        };

        _cache.Set(CacheKey, result, cacheOptions);

        _logger.LogInformation("Successfully fetched and cached court locations from eFiling Hub");

        return result;
    }
}
