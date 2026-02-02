using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Probate.Api.Infrastructure.Authentication
{
    public static class AuthenticationServiceCollectionExtension
    {
        public static IServiceCollection AddProbateAuthentication(
            this IServiceCollection services,
            IWebHostEnvironment env,
            IConfiguration configuration
        )
        {
            services.AddHttpClient();

            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultAuthenticateScheme =
                        CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                })
                .AddCookie(options =>
                {
                    options.Cookie.Name = "ProbateAuth";
                    if (env.IsDevelopment())
                        options.Cookie.Name += ".Development";

                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.None;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

                    options.Events = new CookieAuthenticationEvents
                    {
                        OnRedirectToAccessDenied = context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return context.Response.CompleteAsync();
                        },
                        OnValidatePrincipal = async cookieCtx =>
                        {
                            var accessTokenExpiration = DateTimeOffset.Parse(
                                cookieCtx.Properties.GetTokenValue("expires_at")
                                    ?? DateTimeOffset.UtcNow.ToString()
                            );
                            var timeRemaining = accessTokenExpiration.Subtract(
                                DateTimeOffset.UtcNow
                            );
                            var refreshThreshold = TimeSpan.Parse(
                                configuration["TokenRefreshThreshold"] ?? "00:05:00"
                            );

                            if (timeRemaining > refreshThreshold)
                                return;

                            var refreshToken = cookieCtx.Properties.GetTokenValue("refresh_token");
                            if (string.IsNullOrEmpty(refreshToken))
                                return;

                            var httpClientFactory =
                                cookieCtx.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>();
                            var httpClient = httpClientFactory.CreateClient();

                            var response = await httpClient.RequestRefreshTokenAsync(
                                new RefreshTokenRequest
                                {
                                    Address =
                                        configuration["Keycloak:Authority"]
                                        + "/protocol/openid-connect/token",
                                    ClientId = configuration["Keycloak:Client"],
                                    ClientSecret = configuration["Keycloak:Secret"],
                                    RefreshToken = refreshToken,
                                }
                            );

                            if (response.IsError)
                            {
                                cookieCtx.RejectPrincipal();
                                await cookieCtx.HttpContext.SignOutAsync(
                                    CookieAuthenticationDefaults.AuthenticationScheme
                                );
                            }
                            else
                            {
                                var expiresInSeconds = response.ExpiresIn;
                                var updatedExpiresAt = DateTimeOffset.UtcNow.AddSeconds(
                                    expiresInSeconds
                                );
                                cookieCtx.Properties.UpdateTokenValue(
                                    "expires_at",
                                    updatedExpiresAt.ToString()
                                );
                                cookieCtx.Properties.UpdateTokenValue(
                                    "refresh_token",
                                    response.RefreshToken
                                );
                                cookieCtx.ShouldRenew = true;
                            }
                        },
                    };
                })
                .AddOpenIdConnect(options =>
                {
                    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.Authority = configuration["Keycloak:Authority"];
                    options.ClientId = configuration["Keycloak:Client"];
                    options.ClientSecret = configuration["Keycloak:Secret"];
                    options.RequireHttpsMetadata = !env.IsDevelopment();
                    options.GetClaimsFromUserInfoEndpoint = true;
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.UsePkce = true;
                    options.SaveTokens = true;
                    options.CallbackPath = "/api/auth/signin-oidc";
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");

                    options.Events = new OpenIdConnectEvents
                    {
                        OnTicketReceived = context =>
                        {
                            // Remove id_token and access_token from cookie for security
                            context.Properties.Items.Remove(".Token.id_token");
                            context.Properties.Items.Remove(".Token.access_token");
                            context.Properties.Items[".TokenNames"] =
                                "refresh_token;token_type;expires_at";
                            return Task.CompletedTask;
                        },
                        OnRedirectToIdentityProvider = context =>
                        {
                            // Set the redirect URI explicitly using forwarded headers if available
                            var request = context.HttpContext.Request;

                            // Check for forwarded headers from proxy
                            var forwardedHost = request
                                .Headers["X-Forwarded-Host"]
                                .FirstOrDefault();
                            var forwardedPort = request
                                .Headers["X-Forwarded-Port"]
                                .FirstOrDefault();
                            var forwardedProto =
                                request.Headers["X-Forwarded-Proto"].FirstOrDefault()
                                ?? request.Scheme;

                            string redirectUri;
                            if (!string.IsNullOrEmpty(forwardedHost))
                            {
                                // Use forwarded headers (from proxy like Vite dev server or OpenShift)
                                var port = !string.IsNullOrEmpty(forwardedPort)
                                    ? $":{forwardedPort}"
                                    : "";
                                redirectUri =
                                    $"{forwardedProto}://{forwardedHost}{port}{context.Options.CallbackPath}";
                            }
                            else
                            {
                                // Fallback to request host
                                redirectUri =
                                    $"{request.Scheme}://{request.Host}{context.Options.CallbackPath}";
                            }

                            context.ProtocolMessage.RedirectUri = redirectUri;

                            // Check if kc_idp_hint was set in authentication properties (from login endpoint)
                            if (
                                context.Properties.Items.TryGetValue("kc_idp_hint", out var idpHint)
                            )
                            {
                                context.ProtocolMessage.SetParameter("kc_idp_hint", idpHint);
                            }
                            else
                            {
                                // Fallback to configuration default
                                var kcIdpHint = configuration["Keycloak:KcIdpHint"];
                                context.ProtocolMessage.SetParameter("kc_idp_hint", kcIdpHint);
                            }
                            return Task.CompletedTask;
                        },
                    };
                });

            return services;
        }
    }
}
