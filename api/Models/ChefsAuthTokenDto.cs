namespace Probate.Api.Models;

/// <summary>
/// Response containing CHEFS authentication token and form ID for web component embedding.
/// </summary>
public class ChefsAuthTokenDto
{
    /// <summary>
    /// Short-lived JWT authentication token for the CHEFS web component.
    /// Typically valid for 15 minutes.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// The CHEFS form GUID associated with this token.
    /// </summary>
    public string FormId { get; set; } = string.Empty;

    /// <summary>
    /// The CHEFS base URL the web component must use (matches the token issuer).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
