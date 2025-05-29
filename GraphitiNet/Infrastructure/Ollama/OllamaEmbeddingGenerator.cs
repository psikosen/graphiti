using GraphitiNet.Abstractions;
using GraphitiNet.Configuration;
using GraphitiNet.Exceptions; // Reusing LlmProviderException
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GraphitiNet.Infrastructure.Ollama;

/// <summary>
/// Implements <see cref="IEmbeddingGenerator"/> for interacting with an Ollama service to generate embeddings.
/// </summary>
public class OllamaEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GraphitiNetOptions _options;
    private readonly ILogger<OllamaEmbeddingGenerator> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    // Using the same HttpClient name as OllamaLlmProvider for consistent configuration (e.g., Polly policies)
    public const string OllamaHttpClientName = "OllamaClient";


    public OllamaEmbeddingGenerator(
        IHttpClientFactory httpClientFactory,
        IOptions<GraphitiNetOptions> options,
        ILogger<OllamaEmbeddingGenerator> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options), "GraphitiNetOptions cannot be null.");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(_options.OllamaApiEndpoint))
        {
            throw new ArgumentException("Ollama API endpoint is missing in GraphitiNetOptions.", $"{GraphitiNetOptions.SectionName}:{nameof(_options.OllamaApiEndpoint)}");
        }

        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
    }

    /// <summary>
    /// Represents the request structure for Ollama's /api/embeddings endpoint.
    /// </summary>
    private class OllamaEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("prompt")] // Ollama uses "prompt" for the text input to /api/embeddings
        public required string Prompt { get; set; }

        // Optional: "options" for model-specific parameters, if needed in future
        // [JsonPropertyName("options"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        // public Dictionary<string, object>? ModelParameters { get; set; }
    }

    /// <summary>
    /// Represents the expected response structure from Ollama's /api/embeddings endpoint.
    /// </summary>
    private class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }

    public async Task<float[]?> GenerateEmbeddingAsync(
        string text,
        string? modelName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text)) // Changed from IsNullOrWhiteSpace to allow embedding of whitespace if a model supports it, though generally not recommended.
        {
            _logger.LogWarning("Input text for embedding generation is null or empty.");
            return null; // Or throw ArgumentException, depending on desired strictness
        }

        var effectiveModelName = modelName ?? _options.DefaultEmbeddingModelName;
        if (string.IsNullOrWhiteSpace(effectiveModelName))
        {
            _logger.LogError("No embedding model name provided and no default embedding model configured in GraphitiNetOptions.");
            throw new ArgumentException("Embedding model name must be provided or configured as a default.", nameof(modelName));
        }

        var httpClient = _httpClientFactory.CreateClient(OllamaHttpClientName);
        // BaseAddress for HttpClient "OllamaClient" should be configured in DI.

        var requestUri = new Uri(new Uri(_options.OllamaApiEndpoint.TrimEnd('/') + "/"), "api/embeddings");

        var ollamaRequest = new OllamaEmbeddingRequest
        {
            Model = effectiveModelName,
            Prompt = text
        };

        _logger.LogDebug("Sending request to Ollama for embedding. Model: {Model}, URI: {RequestUri}, Text Preview: {TextPreview}",
            effectiveModelName, requestUri, text.Substring(0, Math.Min(text.Length, 100)) + "...");

        HttpResponseMessage httpResponse;
        string responseContentString = string.Empty;

        try
        {
            var requestContent = JsonContent.Create(ollamaRequest, new MediaTypeHeaderValue("application/json"), _jsonSerializerOptions);
            httpResponse = await httpClient.PostAsync(requestUri, requestContent, cancellationToken);
            
            responseContentString = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Ollama API request for embeddings failed. Status: {StatusCode}, Model: {Model}, URI: {RequestUri}, Response: {ResponseContent}",
                    httpResponse.StatusCode, effectiveModelName, requestUri, responseContentString);
                throw new LlmProviderException( // Reusing LlmProviderException
                    $"Ollama API request for embeddings failed with status code {httpResponse.StatusCode}.",
                    (int)httpResponse.StatusCode,
                    responseContentString);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to Ollama for embeddings failed for model {Model}. URI: {RequestUri}", effectiveModelName, requestUri);
            throw new LlmProviderException($"HTTP request to Ollama for embeddings failed: {ex.Message}", ex.StatusCode?.GetHashCode(), null, ex);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Ollama embeddings request cancelled for model {Model}. URI: {RequestUri}", effectiveModelName, requestUri);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending request to or reading response from Ollama for embeddings for model {Model}. URI: {RequestUri}", effectiveModelName, requestUri);
            throw new LlmProviderException($"Unexpected error during Ollama embeddings request: {ex.Message}", null, responseContentString, ex);
        }

        if (string.IsNullOrWhiteSpace(responseContentString))
        {
            _logger.LogWarning("Received empty response content from Ollama for embeddings despite success status. Model: {Model}, URI: {RequestUri}", effectiveModelName, requestUri);
            return null;
        }

        OllamaEmbeddingResponse? ollamaResponse;
        try
        {
            ollamaResponse = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseContentString, _jsonSerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Ollama's embedding response. Model: {Model}, URI: {RequestUri}, Response: {ResponseContent}",
                effectiveModelName, requestUri, responseContentString);
            throw new LlmProviderException("Failed to deserialize Ollama's embedding response.", (int)httpResponse.StatusCode, responseContentString, ex);
        }

        if (ollamaResponse?.Embedding == null || ollamaResponse.Embedding.Length == 0)
        {
            _logger.LogWarning("Ollama embedding response is null or embedding array is empty. Model: {Model}, URI: {RequestUri}, Full API Response: {FullApiResponse}",
                effectiveModelName, requestUri, responseContentString);
            return null;
        }

        _logger.LogInformation("Successfully generated embedding using model {Model}. Embedding dimensions: {Dimensions}", effectiveModelName, ollamaResponse.Embedding.Length);
        _logger.LogTrace("Generated embedding for model {Model}: [{EmbeddingPreview}...]", effectiveModelName, string.Join(", ", ollamaResponse.Embedding.Take(5)));


        return ollamaResponse.Embedding;
    }
}
```
