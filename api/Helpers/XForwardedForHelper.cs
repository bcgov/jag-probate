using System;

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
    }
}
