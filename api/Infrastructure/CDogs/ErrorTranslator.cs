namespace Probate.Api.Infrastructure.CDogs;

public static class ErrorTranslator
{
    public static string ServiceError(ErrorType errorType, ServiceType serviceType) =>
        $"{errorType}_{serviceType}";
}

public enum ErrorType
{
    CommunicationInternal,
}

public enum ServiceType
{
    CDogs,
}
