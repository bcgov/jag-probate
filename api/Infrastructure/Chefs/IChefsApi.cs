using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace Probate.Api.Infrastructure.Chefs;

/// <summary>
/// Refit client for the CHEFS (Common Hosted Form Service) API.
/// We use form-id (per request) and api-key only; auth-token (JWT) is not used.
/// </summary>
public interface IChefsApi
{
    /// <summary>
    /// Get submissions for a form. Sends form-id as path and header; api-key is added by ChefsApiKeyHandler.
    /// </summary>
    [Get("/forms/{formId}/submissions")]
    Task<ChefsSubmissionsResponse> GetSubmissionsAsync(
        [Header("form-id")] string formId,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Placeholder for CHEFS submissions response. Adjust properties to match CHEFS API response shape.
/// </summary>
public class ChefsSubmissionsResponse
{
    public List<ChefsSubmissionSummary> Submissions { get; set; } = new();
}

public class ChefsSubmissionSummary
{
    public string Id { get; set; } = string.Empty;
    public string FormId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
