namespace Probate.Api.Models.CDogs;

public class CDogsRequestModel
{
    public object Data { get; set; } = new();
    public CDogsOptionsModel Options { get; set; } = new();
    public CDogsTemplateModel Template { get; set; } = new();
}

public class CDogsOptionsModel
{
    public string ReportName { get; set; } = string.Empty;
    public string ConvertTo { get; set; } = "pdf";
    public bool Overwrite { get; set; } = true;
}

public class CDogsTemplateModel
{
    public string Content { get; set; } = string.Empty;
    public string EncodingType { get; set; } = "base64";
    public string FileType { get; set; } = "docx";
}
