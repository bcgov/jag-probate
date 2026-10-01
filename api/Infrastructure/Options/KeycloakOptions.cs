using System;
using System.ComponentModel.DataAnnotations;

namespace Probate.Api.Infrastructure.Options
{
    /// <summary>
    /// Configuration options for Keycloak authentication and OpenID Connect integration.
    /// These options are bound from the "Keycloak" configuration section.
    /// </summary>
    public sealed class KeycloakOptions
    {
        public const string SectionName = "Keycloak";

        public static readonly TimeSpan DefaultRefreshThreshold = TimeSpan.FromMinutes(5);

        public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Keycloak authority URL (e.g., https://keycloak.example.com/auth/realms/your-realm).
        /// This is used as the OpenID Connect provider endpoint.
        /// </summary>
        [Required(ErrorMessage = "Keycloak Authority is required")]
        public string Authority { get; set; } = default!;

        /// <summary>
        /// Keycloak client ID, identifies your application in the Keycloak realm.
        /// </summary>
        [Required(ErrorMessage = "Keycloak Client ID is required")]
        public string Client { get; set; } = default!;

        /// <summary>
        /// Keycloak client secret.
        /// </summary>
        [Required(ErrorMessage = "Keycloak Client Secret is required")]
        public string Secret { get; set; } = default!;

        /// <summary>
        /// Keycloak identity provider hint for automatic provider selection.
        /// </summary>
        public string KcIdpHint { get; set; } = "bceid";

        /// <summary>
        /// (Optional) Refresh threshold (TimeSpan format) for refreshing access tokens.
        /// </summary>
        public string? RefreshThreshold { get; set; }

        /// <summary>
        /// (Optional) Idle timeout (TimeSpan format) for the authentication cookie.
        /// If the user makes no request within this window, the session expires and
        /// they must sign in again. Defaults to <see cref="DefaultIdleTimeout"/> (30 minutes).
        /// </summary>
        public string? IdleTimeout { get; set; }
    }
}
