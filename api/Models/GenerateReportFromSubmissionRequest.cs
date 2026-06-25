namespace Probate.Api.Models;

public class GenerateReportFromSubmissionRequest
{
    /// <summary>
    /// Logical template key, e.g. "P1". Resolved server-side to a .docx file.
    /// </summary>
    public string TemplateKey { get; set; } = string.Empty;

    /// <summary>
    /// The raw CHEFS form submission data to merge into the template.
    /// </summary>
    public object SubmissionData { get; set; } = new();
}
