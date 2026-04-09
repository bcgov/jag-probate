using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Probate.Api.Infrastructure.Options;

namespace Probate.Api.Infrastructure.CDogs
{
    public class CDogsAuthHandler : DelegatingHandler
    {
        private readonly CDogsOptions options;
        private string? cachedToken;
        private DateTime tokenExpiry = DateTime.MinValue;

        public CDogsAuthHandler(IOptions<CDogsOptions> options)
        {
            this.options = options.Value;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            if (string.IsNullOrEmpty(cachedToken) || DateTime.UtcNow >= tokenExpiry)
                await RefreshTokenAsync(ct);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cachedToken);
            return await base.SendAsync(request, ct);
        }

        private async Task RefreshTokenAsync(CancellationToken ct)
        {
            using var client = new HttpClient();
            var body = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret,
                }
            );

            var response = await client.PostAsync(options.TokenUrl, body, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);

            cachedToken = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
            tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 30); // 30s buffer
        }
    }
}
