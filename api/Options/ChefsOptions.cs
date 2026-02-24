using System;
using System.Collections.Generic;

namespace Probate.Api.Options;

/// <summary>
/// Configuration for the CHEFS (Common Hosted Form Service) API.
/// API key is per form; bind from environment (e.g. Chefs__ApiKey, Chefs__BaseUrl).
/// </summary>
public class ChefsOptions
{
    public const string SectionName = "Chefs";

    /// <summary>
    /// Maps logical form keys (sent by the frontend) to actual CHEFS form GUIDs.
    /// The GUID is never exposed to the frontend; callers use the logical key only.
    /// Example env var: Chefs__Forms__probate=&lt;guid&gt;
    /// </summary>
    public Dictionary<string, string> Forms { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// API access key for the form. Required for CHEFS API calls (per-form).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// CHEFS API base URL. Dev: https://chefs-dev.apps.silver.devops.gov.bc.ca/app/api/v1
    /// (Form submit URL is same host: /app/form/submit?f={formId}).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
