using Microsoft.AspNetCore.Http;
using Probate.Api.Helpers;

namespace Probate.Api.Tests.Helpers;

public class XForwardedForHelperTests
{
    private static HttpRequest CreateRequest(string path, string? baseHref = null, string? pathBase = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (pathBase != null)
            context.Request.PathBase = pathBase;
        if (baseHref != null)
            context.Request.Headers["X-Base-Href"] = baseHref;
        return context.Request;
    }

    [Fact]
    public void ApplyForwardedPathBase_WithHeader_SetsPathBaseAndKeepsPath()
    {
        var request = CreateRequest("/api/auth/signin-oidc", "/probate/");

        XForwardedForHelper.ApplyForwardedPathBase(request);

        Assert.Equal("/probate", request.PathBase.Value);
        Assert.Equal("/api/auth/signin-oidc", request.Path.Value);
    }

    [Fact]
    public void ApplyForwardedPathBase_WhenPathAlreadyPrefixed_StripsPrefixFromPath()
    {
        var request = CreateRequest("/probate/api/auth/login", "/probate/");

        XForwardedForHelper.ApplyForwardedPathBase(request);

        Assert.Equal("/probate", request.PathBase.Value);
        Assert.Equal("/api/auth/login", request.Path.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/")]
    [InlineData("/probate/;evil=1/")]
    [InlineData("https://evil.example/")]
    public void ApplyForwardedPathBase_WithMissingOrUnsafeHeader_LeavesRequestUnchanged(string? baseHref)
    {
        var request = CreateRequest("/api/auth/login", baseHref);

        XForwardedForHelper.ApplyForwardedPathBase(request);

        Assert.False(request.PathBase.HasValue);
        Assert.Equal("/api/auth/login", request.Path.Value);
    }

    [Fact]
    public void ApplyForwardedPathBase_WhenPathBaseAlreadySet_DoesNotOverride()
    {
        var request = CreateRequest("/api/auth/login", "/other/", pathBase: "/probate");

        XForwardedForHelper.ApplyForwardedPathBase(request);

        Assert.Equal("/probate", request.PathBase.Value);
        Assert.Equal("/api/auth/login", request.Path.Value);
    }
}
