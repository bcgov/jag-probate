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
        [Authorize(AuthenticationSchemes = OpenIdConnectDefaults.AuthenticationScheme)]
        [HttpGet("login")]
        public IActionResult Login(string returnUrl = "/api")
        {
            var safeReturnUrl = SanitizeReturnUrl(returnUrl);
            return Redirect(safeReturnUrl);
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
            await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);

            var logoutUrl =
                $"{_configuration.GetNonEmptyValue("Keycloak:Authority")}/protocol/openid-connect/logout";

            var forwardedHost = HttpContext.Request.Headers.TryGetValue(
                "X-Forwarded-Host",
                out StringValues forwardedHostValue
            )
                ? forwardedHostValue.ToString()
                : Request.Host.ToString();
            var forwardedPort = HttpContext.Request.Headers["X-Forwarded-Port"];

            //We are always sending X-Forwarded-Port, only time we aren't is when we are hitting the API directly.
            var baseUri = HttpContext.Request.Headers.TryGetValue(
                "X-Base-Href",
                out StringValues baseHrefValue
            )
                ? baseHrefValue.ToString()
                : "/";

            var forwardedProto =
                HttpContext.Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;

            var applicationUrl =
                $"{XForwardedForHelper.BuildUrlString(forwardedHost, forwardedPort, baseUri, scheme: forwardedProto)}";
            var keycloakLogoutUrl = $"{logoutUrl}?post_logout_redirect_uri={applicationUrl}";

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
