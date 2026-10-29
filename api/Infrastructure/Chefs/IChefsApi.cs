using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace Probate.Api.Infrastructure.Chefs;

/// <summary>
/// Refit client for the CHEFS (Common Hosted Form Service) API.
/// Only used to obtain web-component auth tokens; application data is not stored in CHEFS.
/// The auth handler adds Basic Authentication from each form's configured API key.
/// </summary>
public interface IChefsApi
{
    /// <summary>
    /// Get a short-lived auth token (JWT) for the CHEFS web component.
    /// Uses Basic Authentication (formId:apiKey) added by ChefsApiKeyHandler.
    /// </summary>
    [Post("/gateway/v1/auth/token/forms/{formId}")]
    Task<ChefsAuthTokenResponse> GetAuthTokenAsync(
        string formId,
        [Body] ChefsAuthTokenRequest request,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Request body for auth token endpoint.
/// </summary>
public class ChefsAuthTokenRequest
{
    [JsonPropertyName("formId")]
    public string FormId { get; set; } = string.Empty;
}

/// <summary>
/// Placeholder for CHEFS auth token response.
/// </summary>
public class ChefsAuthTokenResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
}
