using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Refit;

namespace Probate.Api.Infrastructure.Chefs;

/// <summary>
/// Refit client for the CHEFS (Common Hosted Form Service) API.
/// The auth handler adds Basic Authentication from each form's configured API key.
/// </summary>
public interface IChefsApi
{
    /// <summary>
    /// Get submissions for a form. Sends form-id as path; authentication is added by ChefsApiKeyHandler.
    /// </summary>
    [Get("/api/v1/forms/{formId}/submissions")]
    Task<List<ChefsSubmissionSummary>> GetSubmissionsAsync(
        string formId,
        CancellationToken cancellationToken = default
    );

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

    /// <summary>
    /// Creates a new submission for a form in CHEFS.
    /// Used during finalization to push step data for platform visibility.
    /// </summary>
    [Post("/api/v1/forms/{formId}/submissions")]
    Task<ChefsCreateSubmissionResponse> CreateSubmissionAsync(
        string formId,
        [Body] ChefsCreateSubmissionRequest body,
        CancellationToken cancellationToken = default
    );

    [Delete("/api/v1/forms/{formId}/submissions/{submissionId}")]
    Task DeleteSubmissionAsync(
        string formId,
        string submissionId,
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

public class ChefsCreateSubmissionRequest
{
    [JsonPropertyName("draft")]
    public bool Draft { get; set; } = false;

    [JsonPropertyName("submission")]
    public ChefsSubmissionPayload Submission { get; set; } = new();
}

public class ChefsSubmissionPayload
{
    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

public class ChefsCreateSubmissionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("confirmationId")]
    public string ConfirmationId { get; set; } = string.Empty;
}

public class ChefsSubmissionSummary
{
    [JsonPropertyName("confirmationId")]
    public string ConfirmationId { get; set; } = string.Empty;

    [JsonPropertyName("formSubmissionStatusCode")]
    public string FormSubmissionStatusCode { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("submissionId")]
    public string SubmissionId { get; set; } = string.Empty;

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [JsonPropertyName("updatedBy")]
    public string UpdatedBy { get; set; } = string.Empty;

    [JsonPropertyName("formVersionId")]
    public string FormVersionId { get; set; } = string.Empty;

    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; } = false;

    [JsonPropertyName("lateEntry")]
    public bool? LateEntry { get; set; }
}
