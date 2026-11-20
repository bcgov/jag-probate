namespace Probate.Api.Models;

public class GenerateReportFromSubmissionRequest
{
    /// <summary>
    /// Logical template key, e.g. "P1". Resolved server-side to an HTML template group.
    /// </summary>
    public string TemplateKey { get; set; } = string.Empty;

    /// <summary>
    /// The raw CHEFS form submission data to merge into the template.
    /// </summary>
    public object SubmissionData { get; set; } = new();

    /// <summary>
    /// Optional IANA timezone of the client browser (e.g. "America/Vancouver"), used to render dates.
    /// Falls back to the server timezone when missing or unrecognized.
    /// </summary>
    public string? TimeZone { get; set; }
}
