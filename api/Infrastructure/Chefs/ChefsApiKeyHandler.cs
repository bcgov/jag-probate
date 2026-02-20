using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Probate.Api.Options;

namespace Probate.Api.Infrastructure.Chefs;

/// <summary>
/// Adds the CHEFS api-key header to every request. We use api-key only (auth-token is not used).
/// form-id is sent per request by the Refit method (path + header).
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
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("api-key", _options.ApiKey);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
