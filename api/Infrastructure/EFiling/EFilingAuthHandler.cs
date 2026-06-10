using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Probate.Api.Infrastructure.Options;

namespace Probate.Api.Infrastructure.EFiling;

/// <summary>
/// HTTP message handler that manages Keycloak authentication for eFiling Hub API
/// Automatically acquires and refreshes OAuth2 tokens using client credentials grant
/// </summary>
public class EFilingAuthHandler : DelegatingHandler
{
    private const string CacheKey = "EFilingAccessToken";
    private readonly EFilingOptions _options;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EFilingAuthHandler> _logger;

    public EFilingAuthHandler(
        IOptions<EFilingOptions> options,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ILogger<EFilingAuthHandler> logger
    )
    {
        _options = options.Value;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct
    )
    {
        // Get token from cache or refresh if expired/missing
        var token = await GetOrRefreshTokenAsync(ct);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, ct);
    }

    private async Task<string> GetOrRefreshTokenAsync(CancellationToken ct)
    {
        // Try to get from cache first
        if (_cache.TryGetValue(CacheKey, out string? cachedToken) && cachedToken != null)
        {
            _logger.LogDebug("Using cached eFiling access token");
            return cachedToken;
        }

        // Cache miss or expired - fetch new token
        return await RefreshTokenAsync(ct);
    }

    private async Task<string> RefreshTokenAsync(CancellationToken ct)
    {
        using var client = _httpClientFactory.CreateClient();

        _logger.LogDebug("Requesting eFiling token");

        // Use Basic Authentication with client_id and client_secret (same as representation-grant)
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}")
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            credentials
        );

        using var body = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["grant_type"] = "client_credentials" }
        );

        var response = await client.PostAsync(_options.TokenUrl, body, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Failed to get eFiling token. Status: {StatusCode}, Response: {Response}",
                response.StatusCode,
                errorContent
            );
            response.EnsureSuccessStatusCode();
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        var token =
            doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Token response missing access_token");
        var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();

        // Cache token with 30s buffer to avoid edge cases
        var cacheExpiry = TimeSpan.FromSeconds(expiresIn - 30);
        _cache.Set(CacheKey, token, cacheExpiry);

        _logger.LogInformation(
            "Successfully obtained eFiling access token, cached for {ExpiresIn}s",
            expiresIn - 30
        );

        return token;
    }
}
