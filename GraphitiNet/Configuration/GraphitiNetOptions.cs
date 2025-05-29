using System.ComponentModel.DataAnnotations;

namespace GraphitiNet.Configuration;

/// <summary>
/// Configuration options for the GraphitiNet library.
/// These options are typically loaded from appsettings.json or other configuration sources.
/// </summary>
public class GraphitiNetOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "GraphitiNet";

    /// <summary>
    /// The URI for the Neo4j database connection.
    /// Example: "neo4j://localhost:7687" or "neo4j+s://yourinstance.databases.neo4j.io"
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Neo4j URI is required.")]
    [Url(ErrorMessage = "Neo4j URI must be a valid URL.")]
    public string Neo4jUri { get; set; } = string.Empty;

    /// <summary>
    /// The username for authenticating with the Neo4j database.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Neo4j user is required.")]
    public string Neo4jUser { get; set; } = string.Empty;

    /// <summary>
    /// The password for authenticating with the Neo4j database.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Neo4j password is required.")]
    public string Neo4jPassword { get; set; } = string.Empty;

    /// <summary>
    /// The target Neo4j database name.
    /// If null or empty, defaults to the user's default database (usually "neo4j" for self-hosted).
    /// For AuraDB, this might be "neo4j" or a specific database name if using multiple databases.
    /// </summary>
    public string? Neo4jDatabase { get; set; } // Optional, defaults handled in Neo4jService

    /// <summary>
    /// The base API endpoint for the Ollama service.
    /// Example: "http://localhost:11434"
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Ollama API endpoint is required.")]
    [Url(ErrorMessage = "Ollama API endpoint must be a valid URL.")]
    public string OllamaApiEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Optional: The default model name to be used for generating embeddings via Ollama.
    /// Example: "nomic-embed-text"
    /// </summary>
    public string? DefaultEmbeddingModelName { get; set; }

    /// <summary>
    /// Optional: The default model name to be used for LLM-based extraction tasks via Ollama.
    /// Example: "llama3" or "mistral"
    /// </summary>
    public string? DefaultExtractionModelName { get; set; }

    // Consider adding a method for more complex validation if DataAnnotations are not sufficient
    // public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    // {
    //    // Custom validation logic here
    //    if (string.IsNullOrWhiteSpace(Neo4jUri))
    //    {
    //        yield return new ValidationResult("Neo4jUri must be provided.", new[] { nameof(Neo4jUri) });
    //    }
    //    // etc.
    // }
}
```
