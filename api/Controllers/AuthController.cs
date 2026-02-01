using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

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
        [AllowAnonymous]
        public IActionResult Login(string returnUrl = "/")
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return Redirect(returnUrl);
            }

            var kcIdpHint = _configuration["Keycloak:KcIdpHint"];

            var properties = new AuthenticationProperties
            {
                RedirectUri = returnUrl,
                IsPersistent = true,
            };

            // Set the kc_idp_hint parameter for BCeID identity provider
            properties.Items["kc_idp_hint"] = kcIdpHint;

            return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
        }

        /// <summary>
        /// Logs out the current user and clears the session
        /// </summary>
        [HttpGet("logout")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var logoutUrl =
                    _configuration["Keycloak:Authority"] + "/protocol/openid-connect/logout";
                var applicationUrl = GetApplicationBaseUrl();

                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);

                var keycloakLogoutUrl = $"{logoutUrl}?post_logout_redirect_uri={applicationUrl}";
                return Redirect(keycloakLogoutUrl);
            }

            return Redirect("/");
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

        private string GetApplicationBaseUrl()
        {
            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
            return baseUrl;
        }
    }
}
