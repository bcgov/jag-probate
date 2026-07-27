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
/// Credentials are attached only for approved CHEFS form API and gateway paths.
/// The CHEFS form GUID is extracted from approved request paths and reverse-looked
/// up against the configured form options to find the matching API key.
/// </summary>
public class ChefsApiKeyHandler : DelegatingHandler
{
    private static readonly Regex FormIdPathRegex = new(
        @"(?:/app)?(?:/api/v1/forms/|/gateway/v1/auth/token/forms/)([^/]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

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
        // Refit route templates must start with '/', which makes them absolute-path references.
        // When combined with a BaseAddress that has a path segment (e.g. ".../app"), the '/app'
        // segment is dropped by Uri resolution. Re-apply the configured base path here so requests
        // hit ".../app/api/v1/..." instead of ".../api/v1/..." (which returns SPA HTML, not JSON).
        request.RequestUri = ApplyBasePath(request.RequestUri);

        var path = request.RequestUri?.AbsolutePath ?? "";

        var formIdMatch = FormIdPathRegex.Match(path);

        if (!formIdMatch.Success)
            throw new InvalidOperationException(
                $"[CHEFS] Request path is not an approved CHEFS form endpoint: {path}"
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

    /// <summary>
    /// Prepends the base path segment from <see cref="ChefsOptions.BaseUrl"/> (e.g. "/app") to the
    /// request URI when it is missing. Refit's absolute-path route templates otherwise strip it.
    /// </summary>
    private Uri? ApplyBasePath(Uri? requestUri)
    {
        if (requestUri is null || string.IsNullOrWhiteSpace(_options.BaseUrl))
            return requestUri;

        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
            return requestUri;

        var basePath = baseUri.AbsolutePath.TrimEnd('/');
        if (basePath.Length == 0)
            return requestUri;

        var currentPath = requestUri.AbsolutePath;
        if (
            currentPath.Equals(basePath, StringComparison.OrdinalIgnoreCase)
            || currentPath.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase)
        )
            return requestUri;

        return new UriBuilder(requestUri) { Path = basePath + currentPath }.Uri;
    }
}
