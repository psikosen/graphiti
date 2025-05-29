using GraphitiNet.Abstractions;
using GraphitiNet.Configuration;
using GraphitiNet.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Headers; // Required for MediaTypeWithQualityHeaderValue
using System.Net.Http.Json; 
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GraphitiNet.Infrastructure.Ollama;

/// <summary>
/// Implements <see cref="ILlmProvider"/> for interacting with an Ollama service.
/// </summary>
public class OllamaLlmProvider : ILlmProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GraphitiNetOptions _options;
    private readonly ILogger<OllamaLlmProvider> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    // Define a constant for the named HttpClient if registered with specific policies
    public const string OllamaHttpClientName = "OllamaClient";

    public OllamaLlmProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<GraphitiNetOptions> options,
        ILogger<OllamaLlmProvider> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options), "GraphitiNetOptions cannot be null.");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(_options.OllamaApiEndpoint))
        {
            // This validation should ideally also be covered by options validation at startup
            throw new ArgumentException("Ollama API endpoint is missing in GraphitiNetOptions.", $"{GraphitiNetOptions.SectionName}:{nameof(_options.OllamaApiEndpoint)}");
        }

        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            // Add any other default serialization options needed, e.g., converters
        };
    }

    /// <summary>
    /// Represents the request structure for Ollama's /api/generate endpoint.
    /// </summary>
    private class OllamaGenerateRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("prompt")]
        public required string Prompt { get; set; }

        [JsonPropertyName("format")]
        public string Format { get; set; } = "json"; // Request JSON output

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false; // MVP does not require streaming
        
        // Optional fields for more control
        [JsonPropertyName("system"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? SystemPrompt { get; set; }

        // Add other Ollama options like "options" for temperature, etc., if needed
        // Example:
        // [JsonPropertyName("options"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        // public Dictionary<string, object>? ModelParameters { get; set; }
    }

    /// <summary>
    /// Represents the expected response structure from Ollama's /api/generate endpoint.
    /// </summary>
    private class OllamaGenerateResponse
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("response")]
        public string Response { get; set; } = string.Empty; // This is where the LLM's JSON string output resides

        [JsonPropertyName("done")]
        public bool Done { get; set; }
        
        // Other fields like "context", "total_duration", etc., are ignored for MVP.
    }

    public async Task<TResponse?> GenerateJsonAsync<TResponse>(
        string prompt,
        string? modelName = null,
        CancellationToken cancellationToken = default) where TResponse : class
    {
        var effectiveModelName = modelName ?? _options.DefaultExtractionModelName;
        if (string.IsNullOrWhiteSpace(effectiveModelName))
        {
            _logger.LogError("No model name provided and no default extraction model configured in GraphitiNetOptions.");
            throw new ArgumentException("LLM model name must be provided or configured as a default.", nameof(modelName));
        }

        var httpClient = _httpClientFactory.CreateClient(OllamaHttpClientName);
        // BaseAddress for the named HttpClient "OllamaClient" should be configured in DI setup.
        // e.g., services.AddHttpClient("OllamaClient", client => { client.BaseAddress = new Uri(options.OllamaApiEndpoint); });
        // If not, ensure the endpoint in options is complete or construct the URI fully here.
        // For this implementation, we assume BaseAddress is set or _options.OllamaApiEndpoint is the full base URI.

        var requestUri = new Uri(new Uri(_options.OllamaApiEndpoint.TrimEnd('/') + "/"), "api/generate");

        var ollamaRequest = new OllamaGenerateRequest
        {
            Model = effectiveModelName,
            Prompt = prompt
            // SystemPrompt could be added here if we design a way to pass it
        };

        _logger.LogDebug("Sending request to Ollama. Model: {Model}, URI: {RequestUri}, Prompt Preview: {PromptPreview}",
            effectiveModelName, requestUri, prompt.Substring(0, Math.Min(prompt.Length, 200)) + "...");

        HttpResponseMessage httpResponse;
        string responseContentString = string.Empty;

        try
        {
            // Ensure correct content type for JSON payload
            var requestContent = JsonContent.Create(ollamaRequest, new MediaTypeHeaderValue("application/json"), _jsonSerializerOptions);
            httpResponse = await httpClient.PostAsync(requestUri, requestContent, cancellationToken);
            
            responseContentString = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Ollama API request failed. Status: {StatusCode}, Model: {Model}, URI: {RequestUri}, Response: {ResponseContent}",
                    httpResponse.StatusCode, effectiveModelName, requestUri, responseContentString);
                throw new LlmProviderException(
                    $"Ollama API request failed with status code {httpResponse.StatusCode}.",
                    (int)httpResponse.StatusCode,
                    responseContentString);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to Ollama failed for model {Model}. URI: {RequestUri}", effectiveModelName, requestUri);
            throw new LlmProviderException($"HTTP request to Ollama failed: {ex.Message}", ex.StatusCode?.GetHashCode(), null, ex);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Ollama request cancelled for model {Model}. URI: {RequestUri}", effectiveModelName, requestUri);
            throw; 
        }
        catch (Exception ex) 
        {
            _logger.LogError(ex, "Unexpected error sending request to or reading response from Ollama for model {Model}. URI: {RequestUri}", effectiveModelName, requestUri);
            throw new LlmProviderException($"Unexpected error during Ollama request: {ex.Message}", null, responseContentString, ex);
        }
        
        if (string.IsNullOrWhiteSpace(responseContentString))
        {
            _logger.LogWarning("Received empty response content from Ollama despite success status. Model: {Model}, URI: {RequestUri}", effectiveModelName, requestUri);
            return null;
        }

        OllamaGenerateResponse? ollamaResponse;
        try
        {
            ollamaResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseContentString, _jsonSerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Ollama's main response object. Model: {Model}, URI: {RequestUri}, Response: {ResponseContent}",
                effectiveModelName, requestUri, responseContentString);
            throw new LlmProviderException("Failed to deserialize Ollama's main response object.", (int)httpResponse.StatusCode, responseContentString, ex);
        }

        if (ollamaResponse == null || string.IsNullOrWhiteSpace(ollamaResponse.Response))
        {
            _logger.LogWarning("Ollama response object or its 'response' field (containing the LLM's JSON string) is empty/null. Model: {Model}, URI: {RequestUri}, Full API Response: {FullApiResponse}",
                effectiveModelName, requestUri, responseContentString);
            return null;
        }

        _logger.LogTrace("Ollama raw LLM JSON string for model {Model}: {LlmJsonOutput}", effectiveModelName, ollamaResponse.Response);

        try
        {
            var finalResponse = JsonSerializer.Deserialize<TResponse>(ollamaResponse.Response, _jsonSerializerOptions);
            if (finalResponse == null)
            {
                 _logger.LogWarning("Deserialization of LLM's JSON output to {TResponse} resulted in null. Model: {Model}, LLM JSON Output: {LlmJsonOutput}",
                    typeof(TResponse).Name, effectiveModelName, ollamaResponse.Response);
            }
            else
            {
                _logger.LogInformation("Successfully deserialized LLM's JSON output to {TResponse}. Model: {Model}", typeof(TResponse).Name, effectiveModelName);
            }
            return finalResponse;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize LLM's JSON output (from 'response' field) to {TResponse}. Model: {Model}, LLM JSON Output: {LlmJsonOutput}",
                typeof(TResponse).Name, effectiveModelName, ollamaResponse.Response);
            throw new LlmProviderException(
                $"Failed to deserialize LLM's JSON output to {typeof(TResponse).Name}. Ensure the LLM prompt requests valid JSON. LLM Output: {ollamaResponse.Response}",
                (int)httpResponse.StatusCode,
                ollamaResponse.Response, 
                ex);
        }
    }
}
```

**Summary of `OllamaLlmProvider.cs` Implementation:**

*   **Dependencies:** Injects `IHttpClientFactory`, `IOptions<GraphitiNetOptions>`, and `ILogger<OllamaLlmProvider>`.
*   **Configuration:** Uses `GraphitiNetOptions` for the Ollama API endpoint and default model names.
*   **`GenerateJsonAsync<TResponse>` Method:**
    *   Determines the `effectiveModelName` to use (parameter or default from options).
    *   Creates an `HttpClient` using `_httpClientFactory.CreateClient(OllamaHttpClientName)`. (Assumes `OllamaHttpClientName` is configured in DI with BaseAddress and Polly policies).
    *   Constructs the request URI for Ollama's `/api/generate` endpoint.
    *   Creates an `OllamaGenerateRequest` object with the model, prompt, `format: "json"`, and `stream: false`.
    *   Serializes the request and POSTs it using `httpClient.PostAsJsonAsync` (or `PostAsync` with `JsonContent`).
    *   Reads and deserializes the `OllamaGenerateResponse`.
    *   Extracts the `response` string from `OllamaGenerateResponse` (this string is the JSON output from the LLM).
    *   Deserializes this inner JSON string into the generic type `TResponse`.
*   **Error Handling:**
    *   Handles `HttpRequestException`, `OperationCanceledException`, and general `Exception` during the HTTP request phase.
    *   Checks for non-success HTTP status codes from Ollama.
    *   Handles `JsonException` during both stages of deserialization (Ollama's main response and the LLM's inner JSON string).
    *   All exceptions are wrapped in `LlmProviderException`, providing context like status codes and response content where available.
*   **Logging:** Includes `ILogger` calls for debugging, information, warnings, and errors throughout the process.
*   **Ollama Request/Response DTOs:** Internal classes `OllamaGenerateRequest` and `OllamaGenerateResponse` are defined to model the structure of the Ollama API interaction.

This implementation provides a complete and robust client for interacting with Ollama to get structured JSON output, as required for the MVP.
