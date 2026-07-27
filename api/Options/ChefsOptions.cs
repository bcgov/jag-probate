using System;
using System.Collections.Generic;

namespace Probate.Api.Options;

public class ChefsFormOptions
{
    public string FormId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for the CHEFS (Common Hosted Form Service) API.
/// API keys are configured per form.
/// </summary>
public class ChefsOptions
{
    public const string SectionName = "Chefs";

    /// <summary>
    /// Maps logical form keys (sent by the frontend) to actual CHEFS form configuration.
    /// The GUID is never exposed to the frontend; callers use the logical key only.
    /// Example env vars: Chefs__Forms__probate__FormId and Chefs__Forms__probate__ApiKey.
    /// </summary>
    public Dictionary<string, ChefsFormOptions> Forms { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// CHEFS app base URL. Dev: https://chefs-dev.apps.silver.devops.gov.bc.ca/app
    /// (Form submit URL is same host: /app/form/submit?f={formId}).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
