using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Probate.Api.Helpers;

namespace Probate.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Initiates the BCeID login flow
        /// </summary>
        /// <param name="returnUrl">Optional URL to redirect to after login</param>
        [HttpGet("login")]
        public IActionResult Login(string returnUrl = "/")
        {
            var safeReturnUrl = SanitizeReturnUrl(returnUrl);

            // Nginx strips the path base (e.g. /probate) before forwarding, so
            // Request.PathBase is empty at the API. Re-attach the base via
            // X-Base-Href so the post-login redirect lands under the correct
            // proxy subpath
            var basePath = XForwardedForHelper.ResolveBaseHref(HttpContext.Request).TrimEnd('/');
            if (
                basePath.Length > 0
                && !safeReturnUrl.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(safeReturnUrl, basePath, StringComparison.OrdinalIgnoreCase)
            )
            {
                safeReturnUrl = basePath + safeReturnUrl;
            }

            if (User.Identity?.IsAuthenticated == true)
                return Redirect(safeReturnUrl);

            var properties = new AuthenticationProperties
            {
                RedirectUri = safeReturnUrl,
            };

            return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
        }

        /// <summary>
        /// Logs out the current user and clears the session
        /// </summary>
        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            // Retrieve id_token BEFORE signing out (sign-out clears the cookie)
            var idToken = await HttpContext.GetTokenAsync("id_token");

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var logoutUrl =
                $"{_configuration.GetNonEmptyValue("Keycloak:Authority")}/protocol/openid-connect/logout";

            // Use the host from forwarded headers (or fall back to Request.Host
            // which already reflects forwarded headers thanks to UseForwardedHeaders).
            // X-Forwarded-Port is intentionally NOT read because on OpenShift the
            // nginx proxy leaks the internal container port (8080).
            var forwardedHost = HttpContext.Request.Headers.TryGetValue(
                "X-Forwarded-Host",
                out StringValues forwardedHostValue
            )
                ? forwardedHostValue.ToString()
                : Request.Host.ToString();

            var baseUri = XForwardedForHelper.ResolveBaseHref(HttpContext.Request);

            var forwardedProto =
                HttpContext.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;

            var applicationUrl =
                $"{XForwardedForHelper.BuildUrlString(forwardedHost, "", baseUri, scheme: forwardedProto)}";
            var clientId = _configuration.GetNonEmptyValue("Keycloak:Client");
            var encodedRedirectUri = Uri.EscapeDataString(applicationUrl);
            var keycloakLogoutUrl =
                $"{logoutUrl}?client_id={clientId}&post_logout_redirect_uri={encodedRedirectUri}";

            if (!string.IsNullOrEmpty(idToken))
            {
                keycloakLogoutUrl += $"&id_token_hint={idToken}";
            }

            return Redirect(keycloakLogoutUrl);
        }

        /// <summary>
        /// Gets the current authenticated user's information
        /// </summary>
        [HttpGet("user")]
        [Authorize]
        public IActionResult GetUserInfo()
        {
            var user = new
            {
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                Name = User.Identity?.Name,
                AuthenticationType = User.Identity?.AuthenticationType,
                Claims = User.Claims.Select(c => new { Type = c.Type, Value = c.Value }),
            };

            return Ok(user);
        }

        /// <summary>
        /// Checks if the user is authenticated
        /// </summary>
        [HttpGet("status")]
        [AllowAnonymous]
        public IActionResult GetAuthStatus()
        {
            return Ok(
                new
                {
                    IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                    Name = User.Identity?.Name,
                }
            );
        }

        /// <summary>
        /// Validates and sanitizes a return URL to prevent open redirect attacks.
        /// </summary>
        private string SanitizeReturnUrl(string returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
                return "/";

            if (Url.IsLocalUrl(returnUrl))
                return returnUrl;

            return "/";
        }
    }
}
