namespace Probate.Api.Helpers;

/// <summary>
/// Sanitizes untrusted values before they are written to log sinks.
/// Prevents log injection attacks where newline characters in user-controlled input
/// could forge additional log entries or corrupt structured log output.
/// </summary>
public static class LogSanitizer
{
    /// <summary>
    /// Replaces CR and LF characters with their escaped representations.
    /// Use this for any value sourced from user input (query params, headers, request body, etc.)
    /// before passing it as a log argument.
    /// </summary>
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
