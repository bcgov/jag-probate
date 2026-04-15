namespace Probate.Api.Models;

public class ReportRequestResult<T>
{
    public ReportResultType ResultStatus { get; set; }
    public T? ResourcePayload { get; set; }
    public ReportRequestResultError? ResultError { get; set; }
    public int TotalResultCount { get; set; }
}

public class ReportRequestResultError
{
    public string ReportResultMessage { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}

public enum ReportResultType
{
    Success,
    Error,
}
