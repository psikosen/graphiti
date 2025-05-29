using GraphitiNet.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GraphitiNet.Abstractions;

/// <summary>
/// Defines the public API contract for interacting with the GraphitiNet client.
/// This client provides methods for ingesting data, extracting graph elements (entities and relationships),
/// and querying the graph.
/// </summary>
public interface IGraphitiClient
{
    /// <summary>
    /// Ingests a piece of text content into the system, creating an EpisodicNode.
    /// This node represents the raw input and serves as a reference for subsequent processing.
    /// </summary>
    /// <param name="content">The main text content to ingest.</param>
    /// <param name="source">Optional identifier for the source of the information (e.g., document URI, message ID).</param>
    /// <param name="referenceTime">
    /// Optional timestamp (UTC) indicating when the content was originally created or relevant (event time).
    /// If null, a UTC timestamp representing the moment of ingestion will be used by default during EpisodicNode creation.
    /// </param>
    /// <param name="properties">Optional additional custom properties to store with the episodic node.</param>
    /// <param name="id">Optional unique identifier for the episodic node. If null, a new GUID will be generated.</param>
    /// <returns>The created <see cref="EpisodicNode"/>.</returns>
    /// <exception cref="System.ArgumentException">Thrown if content is null or whitespace.</exception>
    /// <exception cref="GraphitiNet.Exceptions.GraphDatabaseException">Thrown if errors occur during database operations.</exception>
    Task<EpisodicNode> IngestTextAsync(
        string content,
        string? source = null,
        DateTime? referenceTime = null,
        Dictionary<string, object>? properties = null,
        string? id = null);

    /// <summary>
    /// Extracts entities from the content of a previously ingested EpisodicNode.
    /// Saves the extracted entities to the graph and links them to the source EpisodicNode
    /// via a "MENTIONED_IN_EPISODE" relationship.
    /// </summary>
    /// <param name="episodicNodeId">The ID of the EpisodicNode to process. This node must exist.</param>
    /// <param name="targetEntityLabels">A list of entity labels to target for extraction (e.g., "Person", "Organization").
    /// The LLM will be instructed to classify extracted entities using these labels.</param>
    /// <returns>A list of created or updated <see cref="EntityNode"/>s that were extracted.</returns>
    /// <exception cref="System.ArgumentException">Thrown if episodicNodeId or targetEntityLabels are invalid.</exception>
    /// <exception cref="GraphitiNet.Exceptions.GraphDatabaseException">Thrown if the episodicNode is not found or other database errors occur.</exception>
    /// <exception cref="GraphitiNet.Exceptions.LlmProviderException">Thrown if LLM interaction fails.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if prompt formatting fails or other critical operational errors occur.</exception>
    Task<List<EntityNode>> ExtractEntitiesAsync(string episodicNodeId, List<string> targetEntityLabels);

    /// <summary>
    /// Extracts relationships between a list of specified entities, based on the context of a previously ingested EpisodicNode.
    /// Saves the extracted relationships (EntityEdges) to the graph and applies temporal invalidation logic.
    /// </summary>
    /// <param name="episodicNodeId">The ID of the EpisodicNode providing context for relationship extraction. This node must exist.</param>
    /// <param name="entitiesInContext">A list of <see cref="EntityNode"/>s (typically those extracted from the same episodicNode)
    /// to consider for relationship extraction. Relationships will only be formed between these entities.</param>
    /// <param name="targetRelationshipTypes">A list of relationship types to target for extraction (e.g., "WORKS_FOR", "LOCATED_IN").
    /// The LLM will be instructed to extract relationships matching these types.</param>
    /// <returns>A list of created or updated <see cref="EntityEdge"/>s that were extracted.</returns>
    /// <exception cref="System.ArgumentException">Thrown if episodicNodeId, entitiesInContext, or targetRelationshipTypes are invalid.</exception>
    /// <exception cref="GraphitiNet.Exceptions.GraphDatabaseException">Thrown if the episodicNode is not found or other database errors occur.</exception>
    /// <exception cref="GraphitiNet.Exceptions.LlmProviderException">Thrown if LLM interaction fails.</exception>
    /// <exception cref="System.InvalidOperationException">Thrown if prompt formatting fails or other critical operational errors occur.</exception>
    Task<List<EntityEdge>> ExtractRelationshipsAsync(
        string episodicNodeId,
        List<EntityNode> entitiesInContext,
        List<string> targetRelationshipTypes);

    /// <summary>
    /// Searches for entity nodes semantically similar to the query text.
    /// </summary>
    /// <param name="queryText">The natural language query text.</param>
    /// <param name="limit">The maximum number of similar nodes to return. Defaults to 10.</param>
    /// <param name="targetLabel">Optional: Filter results to include only nodes with this specific label.
    /// For MVP, this filtering may be performed client-side after retrieving results from the database.</param>
    /// <returns>A list of <see cref="EntityNodeSearchResult"/> containing the found nodes and their similarity scores,
    /// ordered by score descending (most similar first).</returns>
    /// <exception cref="System.ArgumentException">Thrown if queryText is null/empty or limit is non-positive.</exception>
    /// <exception cref="GraphitiNet.Exceptions.LlmProviderException">Thrown if embedding generation for the query fails.</exception>
    /// <exception cref="GraphitiNet.Exceptions.GraphDatabaseException">Thrown if errors occur during database search.</exception>
    Task<List<EntityNodeSearchResult>> SearchSimilarNodesAsync(
        string queryText,
        int limit = 10,
        string? targetLabel = null);

    /// <summary>
    /// Searches for entity edges semantically similar to the query text (based on their 'Fact' property).
    /// </summary>
    /// <param name="queryText">The natural language query text.</param>
    /// <param name="limit">The maximum number of similar edges to return. Defaults to 10.</param>
    /// <param name="targetRelationshipType">Optional: Filter results to include only edges with this specific relationship type.
    /// For MVP, this filtering may be performed client-side after retrieving results from the database.</param>
    /// <returns>A list of <see cref="EntityEdgeSearchResult"/> containing the found edges and their similarity scores,
    /// ordered by score descending (most similar first).</returns>
    /// <exception cref="System.ArgumentException">Thrown if queryText is null/empty or limit is non-positive.</exception>
    /// <exception cref="GraphitiNet.Exceptions.LlmProviderException">Thrown if embedding generation for the query fails.</exception>
    /// <exception cref="GraphitiNet.Exceptions.GraphDatabaseException">Thrown if errors occur during database search.</exception>
    Task<List<EntityEdgeSearchResult>> SearchSimilarEdgesAsync(
        string queryText,
        int limit = 10,
        string? targetRelationshipType = null);
}
```

**Summary of `IGraphitiClient` for MVP:**

*   **`IngestTextAsync`**: For initial data capture into an `EpisodicNode`.
*   **`ExtractEntitiesAsync`**: To process an `EpisodicNode`'s content and extract `EntityNode`s.
*   **`ExtractRelationshipsAsync`**: To process an `EpisodicNode`'s content (with context from already extracted `EntityNode`s) and extract `EntityEdge`s.
*   **`SearchSimilarNodesAsync`**: For vector-based search of `EntityNode`s.
*   **`SearchSimilarEdgesAsync`**: For vector-based search of `EntityEdge`s.

This set of methods provides a logical workflow for developers: ingest -> extract entities -> extract relationships -> search. It offers a good balance between control and ease of use for the MVP.
The XML documentation comments also clarify parameters, return types, and potential exceptions, which is important for the public API.The `GraphitiNet/Abstractions/IGraphitiClient.cs` file was updated in the previous turn with the final proposed API.

**Summary of Task 6.1 Deliverables:**

1.  **Proposed `IGraphitiClient` Methods for MVP:**
    *   The following methods have been defined in `GraphitiNet/Abstractions/IGraphitiClient.cs`:
        *   `Task<EpisodicNode> IngestTextAsync(string content, string? source, DateTime? referenceTime, Dictionary<string, object>? properties, string? id)`
        *   `Task<List<EntityNode>> ExtractEntitiesAsync(string episodicNodeId, List<string> targetEntityLabels)`
        *   `Task<List<EntityEdge>> ExtractRelationshipsAsync(string episodicNodeId, List<EntityNode> entitiesInContext, List<string> targetRelationshipTypes)`
        *   `Task<List<EntityNodeSearchResult>> SearchSimilarNodesAsync(string queryText, int limit, string? targetLabel)`
        *   `Task<List<EntityEdgeSearchResult>> SearchSimilarEdgesAsync(string queryText, int limit, string? targetRelationshipType)`
    *   These methods provide a granular, step-by-step approach for MVP, allowing developers to control each phase of graph construction and querying.

2.  **Defined Workflow for Graph Building:**
    *   The typical workflow is:
        1.  Call `IngestTextAsync` to create an `EpisodicNode` from raw text.
        2.  Call `ExtractEntitiesAsync` with the `EpisodicNode.Id` and target entity labels to extract and store `EntityNode`s.
        3.  Call `ExtractRelationshipsAsync` with the `EpisodicNode.Id`, the list of `EntityNode`s from step 2, and target relationship types to extract and store `EntityEdge`s.

3.  **Confirmed Entry Points for Configuration:**
    *   Configuration will be managed via Dependency Injection and the Options pattern (`GraphitiNetOptions`), as designed in Task 2.3.
    *   Users will configure the library through an extension method like `services.AddGraphitiNetServices(configuration)` and provide settings in `appsettings.json` or other .NET configuration sources.

4.  **Other Public Types:**
    *   **Models (`GraphitiNet.Models`):** `EpisodicNode`, `EntityNode`, `EntityEdge`, `EntityNodeSearchResult`, `EntityEdgeSearchResult`. `BaseNode` and `BaseEdge` might also be public if users derive from them or interact with their properties, but direct use is less common.
    *   **Exceptions (`GraphitiNet.Exceptions`):** `GraphitiNetException`, `LlmProviderException`, `GraphDatabaseException`.
    *   **Enums:** None are directly part of `IGraphitiClient` method signatures for MVP, but `EdgeDirection` exists in `GraphitiNet.Abstractions` for `IGraphDatabaseService`.

The public API surface for the MVP is well-defined, focusing on a clear, step-by-step process for graph building and querying, which aligns with the project's initial goals.
