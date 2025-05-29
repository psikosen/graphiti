using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GraphitiNet.Models;

/// <summary>
/// Abstract base record for all edge (relationship) types in GraphitiNet.
/// Provides common properties shared by all edges.
/// </summary>
public abstract record BaseEdge
{
    /// <summary>
    /// Unique identifier for the edge. Typically a UUID string.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// The unique identifier of the source (or "from") node of this edge.
    /// </summary>
    [JsonPropertyName("fromNodeId")]
    public required string FromNodeId { get; init; }

    /// <summary>
    /// The unique identifier of the target (or "to") node of this edge.
    /// </summary>
    [JsonPropertyName("toNodeId")]
    public required string ToNodeId { get; init; }

    /// <summary>
    /// The type of the relationship (e.g., "KNOWS", "WORKS_AT", "MENTIONS_ENTITY").
    /// This typically maps to the relationship type string in Neo4j.
    /// </summary>
    [JsonPropertyName("relationshipType")]
    public required string RelationshipType { get; init; }

    /// <summary>
    /// Timestamp (UTC) indicating when the edge was created in the GraphitiNet system.
    /// Defaults to the time of object instantiation.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// A dictionary for storing additional, dynamic properties associated with the edge.
    /// This allows for schema flexibility beyond the explicitly defined properties.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; init; } = new Dictionary<string, object>();
}
