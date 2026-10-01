using System.Collections.Generic;

namespace Probate.Api.Models;

public class PdfGenerationRequest
{
    public IReadOnlyList<string> Documents { get; set; } = [];
    public string FileName { get; set; } = string.Empty;
}
