using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using Probate.Api.Infrastructure.Chefs;
using Probate.Api.Options;

namespace Probate.Api.Tests.Infrastructure;

public class ChefsApiKeyHandlerTests
{
    private const string LegalFormId = "11111111-1111-1111-1111-111111111111";
    private const string LegalApiKey = "legal-api-key";
    private const string NonLegalFormId = "22222222-2222-2222-2222-222222222222";

    [Theory]
    [InlineData("/api/v1/forms/11111111-1111-1111-1111-111111111111/submissions")]
    [InlineData("/gateway/v1/auth/token/forms/11111111-1111-1111-1111-111111111111")]
    public async Task SendAsync_WithConfiguredForm_AddsBasicAuthorization(string path)
    {
        var captureHandler = new CaptureHandler();
        using var invoker = CreateInvoker(captureHandler);
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://chefs.example.com{path}");

        await invoker.SendAsync(request, CancellationToken.None);

        Assert.NotNull(captureHandler.Request);
        Assert.Equal("Basic", captureHandler.Request.Headers.Authorization?.Scheme);
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{LegalFormId}:{LegalApiKey}")),
            captureHandler.Request.Headers.Authorization?.Parameter
        );
    }

    [Fact]
    public async Task SendAsync_WithDifferentConfiguredForm_UsesMatchingApiKey()
    {
        var captureHandler = new CaptureHandler();
        using var invoker = CreateInvoker(captureHandler);
        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"https://chefs.example.com/api/v1/forms/{NonLegalFormId}/submissions/submission-id"
        );

        await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{NonLegalFormId}:nonlegal-api-key")),
            captureHandler.Request?.Headers.Authorization?.Parameter
        );
    }

    [Fact]
    public async Task SendAsync_WithBlankApiKey_ThrowsClearException()
    {
        using var invoker = CreateInvoker(
            new CaptureHandler(),
            new ChefsOptions
            {
                Forms =
                {
                    ["legal"] = new ChefsFormOptions { FormId = LegalFormId, ApiKey = " " },
                },
            }
        );
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://chefs.example.com/api/v1/forms/{LegalFormId}/submissions"
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invoker.SendAsync(request, CancellationToken.None)
        );

        Assert.Contains("API key is not configured", exception.Message);
    }

    [Fact]
    public async Task SendAsync_WithUnconfiguredForm_ThrowsClearException()
    {
        using var invoker = CreateInvoker(new CaptureHandler());
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://chefs.example.com/api/v1/forms/33333333-3333-3333-3333-333333333333/submissions"
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invoker.SendAsync(request, CancellationToken.None)
        );

        Assert.Contains("No form configuration found", exception.Message);
    }

    private static HttpMessageInvoker CreateInvoker(
        HttpMessageHandler innerHandler,
        ChefsOptions? options = null
    )
    {
        var handler = new ChefsApiKeyHandler(
            Microsoft.Extensions.Options.Options.Create(options ?? CreateOptions())
        )
        {
            InnerHandler = innerHandler,
        };

        return new HttpMessageInvoker(handler);
    }

    private static ChefsOptions CreateOptions()
    {
        return new ChefsOptions
        {
            Forms =
            {
                ["legal"] = new ChefsFormOptions { FormId = LegalFormId, ApiKey = LegalApiKey },
                ["nonlegal"] = new ChefsFormOptions
                {
                    FormId = NonLegalFormId,
                    ApiKey = "nonlegal-api-key",
                },
            },
        };
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
