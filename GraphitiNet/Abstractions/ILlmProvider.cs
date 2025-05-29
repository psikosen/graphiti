using System.Threading;
using System.Threading.Tasks;

namespace GraphitiNet.Abstractions;

/// <summary>
/// Defines the contract for an LLM (Large Language Model) provider.
/// This interface abstracts the specifics of interacting with different LLM backends.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Generates a structured JSON response from the LLM based on the given prompt.
    /// </summary>
    /// <typeparam name="TResponse">The type to deserialize the LLM's JSON response into.</typeparam>
    /// <param name="prompt">The input prompt to send to the LLM.</param>
    /// <param name="modelName">
    /// Optional name of the model to use. If null or empty, the provider should use a default model
    /// configured in <see cref="GraphitiNet.Configuration.GraphitiNetOptions"/>.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the deserialized
    /// response of type <typeparamref name="TResponse"/>, or null if the operation failed,
    /// the response was empty, or the response could not be deserialized.
    /// </returns>
    /// <exception cref="GraphitiNet.Exceptions.LlmProviderException">
    /// Thrown if an error occurs while communicating with the LLM provider or processing its response.
    /// </exception>
    Task<TResponse?> GenerateJsonAsync<TResponse>(
        string prompt,
        string? modelName = null,
        CancellationToken cancellationToken = default) where TResponse : class;
}
```
