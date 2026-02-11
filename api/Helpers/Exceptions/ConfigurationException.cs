using System;

namespace Probate.Api.Helpers.Exceptions
{
    /// <summary>
    /// ConfigurationException class, provides a way to throw an exception when a configuration is invalid.
    /// </summary>
    public class ConfigurationException : Exception
    {
        /// <summary>
        /// Creates a new instance of a ConfigurationException class.
        /// </summary>
        public ConfigurationException() { }

        /// <summary>
        /// Creates a new instance of a ConfigurationException class, and initializes it with the specified arguments.
        /// </summary>
        /// <param name="message"></param>
        public ConfigurationException(string message)
            : base(message) { }

        /// <summary>
        /// Creates a new instance of a ConfigurationException class, and initializes it with the specified arguments.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public ConfigurationException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
