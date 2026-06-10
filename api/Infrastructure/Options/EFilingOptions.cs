using System.ComponentModel.DataAnnotations;
using Probate.Api.Models.EFiling;

namespace Probate.Api.Infrastructure.Options;

/// <summary>
/// Configuration options for eFiling Hub API integration
/// </summary>
public class EFilingOptions
{
    public const string SectionName = "EFiling";

    /// <summary>
    /// eFiling Hub API base URL (e.g., https://nginx-fc726a-dev.apps.silver.devops.gov.bc.ca/api)
    /// </summary>
    [Required]
    public required string BaseUrl { get; set; }

    /// <summary>
    /// Court level (P = Provincial, S = Supreme)
    /// Default: Supreme Court (matching representation-grant)
    /// </summary>
    public CourtLevelEnum CourtLevel { get; set; } = CourtLevelEnum.S;

    /// <summary>
    /// Keycloak base URL (e.g., https://dev.loginproxy.gov.bc.ca)
    /// </summary>
    [Required]
    public required string KeycloakBaseUrl { get; set; }

    /// <summary>
    /// Keycloak realm (e.g., "court-services-jag")
    /// </summary>
    [Required]
    public required string KeycloakRealm { get; set; }

    /// <summary>
    /// Keycloak client ID for service account authentication
    /// </summary>
    [Required]
    public required string ClientId { get; set; }

    /// <summary>
    /// Keycloak client secret for service account authentication
    /// </summary>
    [Required]
    public required string ClientSecret { get; set; }

    /// <summary>
    /// Whether eFiling integration is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Computed token URL from base URL and realm
    /// </summary>
    public string TokenUrl =>
        $"{KeycloakBaseUrl.TrimEnd('/')}/auth/realms/{KeycloakRealm}/protocol/openid-connect/token";
}
