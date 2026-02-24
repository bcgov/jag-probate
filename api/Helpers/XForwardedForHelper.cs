using System;
using Microsoft.AspNetCore.Http;

namespace Probate.Api.Helpers
{
    public static class XForwardedForHelper
    {
        public static string BuildUrlString(
            string forwardedHost,
            string forwardedPort,
            string baseUrl,
            string remainingPath = "",
            string query = "",
            string scheme = "https"
        )
        {
            var sanitizedPath = baseUrl;
            if (!string.IsNullOrEmpty(remainingPath))
            {
                sanitizedPath = string.Format(
                    "{0}/{1}",
                    baseUrl.TrimEnd('/'),
                    remainingPath.TrimStart('/')
                );
            }

            // forwardedHost may contain a port (e.g. "localhost:8080").
            // UriBuilder.Host expects a bare hostname, so split if needed.
            var hostOnly = forwardedHost;
            if (
                string.IsNullOrEmpty(forwardedPort)
                && forwardedHost != null
                && forwardedHost.Contains(":")
            )
            {
                var parts = forwardedHost.Split(':', 2);
                hostOnly = parts[0];
                forwardedPort = parts[1];
            }

            var uriBuilder = new UriBuilder
            {
                Scheme = scheme,
                Host = hostOnly,
                Path = sanitizedPath,
                Query = query,
            };

            if (
                !string.IsNullOrEmpty(forwardedPort)
                && forwardedPort != "80"
                && forwardedPort != "443"
                && int.TryParse(forwardedPort, out var port)
            )
            {
                uriBuilder.Port = port;
            }

            return uriBuilder.Uri.AbsoluteUri;
        }

        public static void ApplyPathBase(HttpRequest request)
        {
            if (request == null)
                return;

            var baseHref = ResolveBaseHref(request);
            if (baseHref == "/")
                return;

            var pathBase = new PathString(baseHref.TrimEnd('/'));
            if (request.Path.StartsWithSegments(pathBase, out var remaining))
            {
                request.PathBase = pathBase;
                request.Path = remaining;
            }
        }

        public static string ResolveBaseHref(HttpRequest request)
        {
            if (request == null)
                return "/";

            if (request.Headers.TryGetValue("X-Base-Href", out var baseHrefValues))
            {
                var normalized = NormalizeBaseHref(baseHrefValues.ToString());
                if (normalized != "/")
                    return normalized;
            }

            if (request.PathBase.HasValue && !string.IsNullOrWhiteSpace(request.PathBase.Value))
            {
                return NormalizeBaseHref(request.PathBase.Value);
            }

            var pathValue = request.Path.Value;
            if (!string.IsNullOrEmpty(pathValue))
            {
                var segments = pathValue.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (
                    segments.Length > 1
                    && string.Equals(segments[1], "api", StringComparison.OrdinalIgnoreCase)
                )
                {
                    return NormalizeBaseHref($"/{segments[0]}/");
                }
            }

            return "/";
        }

        public static string NormalizeBaseHref(string baseHref)
        {
            var trimmed = baseHref?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return "/";

            if (!trimmed.StartsWith("/"))
                trimmed = "/" + trimmed;

            if (!trimmed.EndsWith("/"))
                trimmed += "/";

            return trimmed;
        }
    }
}
