using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using Probate.Api.Infrastructure.Chefs;
using Probate.Api.Options;

namespace Probate.Api.Tests.Infrastructure;

public class ChefsApiKeyHandlerTests
{
    private const string PrimaryFormId = "11111111-1111-1111-1111-111111111111";
    private const string PrimaryApiKey = "primary-api-key";
    private const string SecondaryFormId = "22222222-2222-2222-2222-222222222222";

    [Theory]
    [InlineData("/api/v1/forms/11111111-1111-1111-1111-111111111111/submissions")]
    [InlineData("/app/api/v1/forms/11111111-1111-1111-1111-111111111111/submissions")]
    [InlineData("/gateway/v1/auth/token/forms/11111111-1111-1111-1111-111111111111")]
    [InlineData("/app/gateway/v1/auth/token/forms/11111111-1111-1111-1111-111111111111")]
    public async Task SendAsync_WithConfiguredForm_AddsBasicAuthorization(string path)
    {
        var captureHandler = new CaptureHandler();
        using var invoker = CreateInvoker(captureHandler);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://chefs.example.com{path}"
        );

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.NotNull(captureHandler.Request);
        Assert.Equal("Basic", captureHandler.Request.Headers.Authorization?.Scheme);
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{PrimaryFormId}:{PrimaryApiKey}")),
            captureHandler.Request.Headers.Authorization?.Parameter
        );
    }

    [Fact]
    public async Task SendAsync_WithDifferentConfiguredForm_UsesMatchingApiKey()
    {
        var captureHandler = new CaptureHandler();
        using var invoker = CreateInvoker(captureHandler);
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"https://chefs.example.com/api/v1/forms/{SecondaryFormId}/submissions/submission-id"
        );

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SecondaryFormId}:secondary-api-key")),
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
                    ["form-primary"] = new ChefsFormOptions { FormId = PrimaryFormId, ApiKey = " " },
                },
            }
        );
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://chefs.example.com/api/v1/forms/{PrimaryFormId}/submissions"
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
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://chefs.example.com/api/v1/forms/33333333-3333-3333-3333-333333333333/submissions"
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invoker.SendAsync(request, CancellationToken.None)
        );

        Assert.Contains("No form configuration found", exception.Message);
    }

    [Fact]
    public async Task SendAsync_WithUnapprovedPath_ThrowsClearException()
    {
        using var invoker = CreateInvoker(new CaptureHandler());
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://chefs.example.com/app/admin/forms/{PrimaryFormId}/submissions"
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invoker.SendAsync(request, CancellationToken.None)
        );

        Assert.Contains("not an approved CHEFS form endpoint", exception.Message);
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
                ["form-primary"] = new ChefsFormOptions { FormId = PrimaryFormId, ApiKey = PrimaryApiKey },
                ["form-secondary"] = new ChefsFormOptions
                {
                    FormId = SecondaryFormId,
                    ApiKey = "secondary-api-key",
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
#pragma warning disable CA2000
            // Dummy response is handled by the invoker, so we don't need to dispose it here.
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
#pragma warning restore CA2000
        }
    }
}
