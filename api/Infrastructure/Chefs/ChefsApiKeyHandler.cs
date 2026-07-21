using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Probate.Api.Options;

namespace Probate.Api.Infrastructure.Chefs;

/// <summary>
/// Adds Basic Authentication to every CHEFS request using the per-form API key.
/// The CHEFS form GUID is extracted from the request path and reverse-looked up
/// against the configured form options to find the matching API key.
/// </summary>
public class ChefsApiKeyHandler : DelegatingHandler
{
    private readonly ChefsOptions _options;

    public ChefsApiKeyHandler(IOptions<ChefsOptions> options)
    {
        _options = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var path = request.RequestUri?.AbsolutePath ?? "";

        // Matches /api/v1/forms/{formId}/... or /gateway/v1/auth/token/forms/{formId}/...
        var formIdMatch = Regex.Match(
            path,
            @"(?:/api/v1/forms/|/gateway/v1/auth/token/forms/)([^/]+)",
            RegexOptions.IgnoreCase
        );

        if (!formIdMatch.Success)
            throw new InvalidOperationException(
                $"[CHEFS] Could not extract a formId from request path: {path}"
            );

        var formId = formIdMatch.Groups[1].Value;

        // Reverse lookup: the path contains the CHEFS GUID; Forms is keyed by logical name.
        var formOptions = _options.Forms.Values.FirstOrDefault(f =>
            string.Equals(f.FormId, formId, StringComparison.OrdinalIgnoreCase)
        );

        if (formOptions is null)
            throw new InvalidOperationException(
                $"[CHEFS] No form configuration found for formId '{formId}'. Check Chefs:Forms in configuration."
            );

        if (string.IsNullOrWhiteSpace(formOptions.ApiKey))
            throw new InvalidOperationException(
                $"[CHEFS] API key is not configured for formId '{formId}'. Check Chefs:Forms configuration."
            );

        var authValue = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{formId}:{formOptions.ApiKey}")
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            System.Diagnostics.Debug.WriteLine(
                $"[CHEFS] API Error ({response.StatusCode}) URL={request.RequestUri} Content={content.Substring(0, Math.Min(500, content.Length))}"
            );
        }

        return response;
    }
}
