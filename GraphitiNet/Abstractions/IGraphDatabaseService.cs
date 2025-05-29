using GraphitiNet.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GraphitiNet.Abstractions;

/// <summary>
/// Defines the contract for interacting with the graph database.
/// This service is responsible for all Create, Read, Update, Delete (CRUD) operations
/// for nodes and edges, as well as search functionalities.
/// </summary>
public interface IGraphDatabaseService
{
    /// <summary>
    /// Adds or updates an entity node in the graph database.
    /// If a node with the same ID already exists, it should be updated (merged).
    /// </summary>
    /// <param name="node">The entity node to add or update.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddOrUpdateEntityNodeAsync(EntityNode node);

    /// <summary>
    /// Adds or updates an episodic node in the graph database.
    /// If a node with the same ID already exists, it should be updated (merged).
    /// </summary>
    /// <param name="node">The episodic node to add or update.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddOrUpdateEpisodicNodeAsync(EpisodicNode node);

    /// <summary>
    /// Adds or updates an edge (of any type inheriting from BaseEdge) in the graph database.
    /// If an edge with the same ID already exists, its properties should be updated.
    /// The source and target nodes of the edge are assumed to exist.
    /// </summary>
    /// <param name="edge">The edge to add or update.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddOrUpdateEdgeAsync(BaseEdge edge);

    /// <summary>
    /// Retrieves an entity node by its unique identifier.
    /// </summary>
    /// <param name="nodeId">The ID of the entity node to retrieve.</param>
    /// <returns>A task that represents the asynchronous operation.
    /// The task result contains the <see cref="EntityNode"/> if found; otherwise, null.</returns>
    Task<EntityNode?> GetEntityNodeAsync(string nodeId);

    /// <summary>
    /// Retrieves an entity edge by its unique identifier.
    /// Note: This method is specifically for EntityEdge. For generic edge retrieval,
    /// one might need a different approach or ensure ID is globally unique across edge types.
    /// </summary>
    /// <param name="edgeId">The ID of the entity edge to retrieve.</param>
    /// <returns>A task that represents the asynchronous operation.
    /// The task result contains the <see cref="EntityEdge"/> if found; otherwise, null.</returns>
    Task<EntityEdge?> GetEntityEdgeAsync(string edgeId);

    /// <summary>
    /// Searches for entity nodes using vector similarity on their name embeddings.
    /// </summary>
    /// <param name="indexName">The name of the vector index in Neo4j to search against.</param>
    /// <param name="embeddingPropertyKey">
    /// The property key of the embedding on the node (e.g., "nameEmbedding").
    /// Note: For Neo4j native vector indexes, this key is part of the index definition
    /// and might not be explicitly needed in the query call itself if the index is correctly configured.
    /// </param>
    /// <param name="vector">The query vector for similarity search.</param>
    /// <param name="limit">The maximum number of nodes to return.</param>
    /// <returns>A task that represents the asynchronous operation.
    /// The task result contains an enumerable of tuples, each containing an <see cref="EntityNode"/>
    /// and its similarity score.</returns>
    Task<IEnumerable<(EntityNode Node, float Score)>> SearchNodesByVectorAsync(string indexName, string embeddingPropertyKey, float[] vector, int limit);

    /// <summary>
    /// Searches for entity edges using vector similarity on their fact embeddings.
    /// </summary>
    /// <param name="indexName">The name of the vector index in Neo4j to search against.</param>
    /// <param name="embeddingPropertyKey">
    /// The property key of the embedding on the edge (e.g., "factEmbedding").
    /// Note: Similar to node search, this key is part of the Neo4j index definition.
    /// </param>
    /// <param name="vector">The query vector for similarity search.</param>
    /// <param name="limit">The maximum number of edges to return.</param>
    /// <returns>A task that represents the asynchronous operation.
    /// The task result contains an enumerable of tuples, each containing an <see cref="EntityEdge"/>
    /// and its similarity score.</returns>
    Task<IEnumerable<(EntityEdge Edge, float Score)>> SearchEdgesByVectorAsync(string indexName, string embeddingPropertyKey, float[] vector, int limit);

    /// <summary>
    /// Retrieves entity edges connected to a specific entity node.
    /// </summary>
    /// <param name="nodeId">The ID of the entity node.</param>
    /// <param name="direction">The direction of edges to retrieve (Outgoing, Incoming, Both).</param>
    /// <param name="relationshipTypes">Optional filter by specific relationship types. If null or empty, retrieves all types.</param>
    /// <returns>A task that represents the asynchronous operation.
    /// The task result contains an enumerable of connected <see cref="EntityEdge"/>s.</returns>
    Task<IEnumerable<EntityEdge>> GetConnectedEdgesAsync(string nodeId, EdgeDirection direction = EdgeDirection.Both, params string[]? relationshipTypes);

    /// <summary>
    /// Finds and invalidates existing EntityEdges that directly conflict with a new edge being added.
    /// A conflict is defined as an existing edge connecting the same source and target nodes
    /// with the same relationship type, which is currently valid (invalid_at IS NULL)
    /// and whose validity (valid_at) started before the new edge's asserted validity.
    /// </summary>
    /// <param name="fromNodeId">The ID of the source node of the new/conflicting edge.</param>
    /// <param name="toNodeId">The ID of the target node of the new/conflicting edge.</param>
    /// <param name="relationshipType">The type of the relationship of the new/conflicting edge.</param>
    /// <param name="newEdgeValidAt">
    /// The time at which the new, potentially conflicting, edge becomes valid.
    /// Existing edges that are currently valid and have a valid_at timestamp strictly
    /// earlier than this time (or a null valid_at) will be considered for invalidation.
    /// This timestamp will also be used to set the 'invalid_at' field on the conflicting edges.
    /// </param>
    /// <returns>A task representing the asynchronous operation, returning the number of edges invalidated.</returns>
    Task<int> InvalidateConflictingEdgesAsync(
        string fromNodeId,
        string toNodeId,
        string relationshipType,
        DateTime newEdgeValidAt);
}

/// <summary>
/// Specifies the direction of edges to retrieve relative to a node.
/// </summary>
public enum EdgeDirection
{
    /// <summary>
    /// Retrieve only outgoing edges from the node.
    /// </summary>
    Outgoing,
    /// <summary>
    /// Retrieve only incoming edges to the node.
    /// </summary>
    Incoming,
    /// <summary>
    /// Retrieve both incoming and outgoing edges.
    /// </summary>
    Both
}
```
