using System.Threading;
using System.Threading.Tasks;

namespace GraphitiNet.Abstractions;

/// <summary>
/// Defines the contract for an embedding generator.
/// This interface abstracts the specifics of interacting with different embedding model backends.
/// </summary>
public interface IEmbeddingGenerator
{
    /// <summary>
    /// Generates an embedding vector for the given text.
    /// </summary>
    /// <param name="text">The input text to embed.</param>
    /// <param name="modelName">
    /// Optional name of the embedding model to use. If null or empty, the provider should use a default model
    /// configured in <see cref="GraphitiNet.Configuration.GraphitiNetOptions"/>.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains a nullable
    /// array of floats (`float[]?`) representing the embedding vector. Returns null if the
    /// operation failed or an embedding could not be generated.
    /// </returns>
    /// <exception cref="GraphitiNet.Exceptions.LlmProviderException">
    /// Thrown if an error occurs while communicating with the embedding provider or processing its response.
    /// This exception (or a derived one like EmbeddingProviderException) is used for consistency.
    /// </exception>
    Task<float[]?> GenerateEmbeddingAsync(
        string text,
        string? modelName = null,
        CancellationToken cancellationToken = default);
}
```
