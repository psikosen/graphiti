using System;

namespace GraphitiNet.Exceptions;

/// <summary>
/// Represents errors that occur during interactions with an LLM provider.
/// </summary>
public class LlmProviderException : Exception
{
    public LlmProviderException()
    {
    }

    public LlmProviderException(string message) : base(message)
    {
    }

    public LlmProviderException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmProviderException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="statusCode">The HTTP status code from the LLM provider, if applicable.</param>
    /// <param name="responseContent">The response content from the LLM provider, if applicable.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public LlmProviderException(string message, int? statusCode, string? responseContent, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
    }

    /// <summary>
    /// Gets the HTTP status code returned by the LLM provider, if applicable.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Gets the response content from the LLM provider, if applicable.
    /// </summary>
    public string? ResponseContent { get; }
}
```
