using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly EFilingOptions _options;
    private readonly ILogger<EFilingAuthHandler> _logger;
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public EFilingAuthHandler(IOptions<EFilingOptions> options, ILogger<EFilingAuthHandler> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct
    )
    {
        // Refresh token if expired or not cached
        if (string.IsNullOrEmpty(_cachedToken) || DateTime.UtcNow >= _tokenExpiry)
            await RefreshTokenAsync(ct);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);
        return await base.SendAsync(request, ct);
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        using var client = new HttpClient();

        _logger.LogDebug("Requesting eFiling token");

        // Use Basic Authentication with client_id and client_secret (same as representation-grant)
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}")
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var body = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            }
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
        var doc = JsonDocument.Parse(json);

        _cachedToken = doc.RootElement.GetProperty("access_token").GetString();
        var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 30); // 30s buffer to avoid edge cases

        _logger.LogInformation("Successfully obtained eFiling access token, expires in {ExpiresIn}s", expiresIn);
    }
}
