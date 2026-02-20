using System;
using System.Net;

namespace Probate.Api.Helpers.Exceptions;

/// <summary>
/// Thrown when the CHEFS API returns an error. Used to propagate status code and message to the API response.
/// </summary>
public class ChefsApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public ChefsApiException(string message, HttpStatusCode statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public ChefsApiException(string message, HttpStatusCode statusCode, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
