using System.ComponentModel.DataAnnotations;

namespace Probate.Api.Infrastructure.Options;

public class WeasyPrintOptions
{
    public const string SectionName = "WeasyPrint";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;
}
