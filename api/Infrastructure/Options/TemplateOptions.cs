using System.Collections.Generic;

namespace Probate.Api.Infrastructure.Options;

public class TemplateOptions
{
    public const string SectionName = "Templates";
    public Dictionary<string, string[]> Groups { get; set; } = new();
}
