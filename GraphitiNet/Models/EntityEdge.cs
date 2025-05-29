using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GraphitiNet.Models;

/// <summary>
/// Represents a factual relationship between two <see cref="EntityNode"/>s in the knowledge graph.
/// It describes how two entities are connected and provides temporal context for that connection.
/// </summary>
public record EntityEdge : BaseEdge
{
    /// <summary>
    /// A natural language description of the fact or relationship represented by this edge.
    /// (e.g., "Person A works for Company B", "Event X occurred at Location Y").
    /// </summary>
    [JsonPropertyName("fact")]
    public required string Fact { get; init; }

    /// <summary>
    /// Optional vector embedding of the <see cref="Fact"/> text.
    /// Used for semantic search to find edges based on the meaning of their described relationship.
    /// </summary>
    [JsonPropertyName("factEmbedding")]
    public float[]? FactEmbedding { get; init; }

    /// <summary>
    /// Optional timestamp (UTC) indicating when the fact or relationship
    /// became true or valid in the real world (event time start).
    /// </summary>
    [JsonPropertyName("validAt")]
    public DateTime? ValidAt { get; init; }

    /// <summary>
    /// Optional timestamp (UTC) indicating when the fact or relationship
    /// ceased to be true or valid in the real world (event time end).
    /// A null value typically means the relationship is currently valid or its end is unknown.
    /// </summary>
    [JsonPropertyName("invalidAt")]
    public DateTime? InvalidAt { get; init; }

    /// <summary>
    /// An optional list of unique identifiers (UUIDs) of <see cref="EpisodicNode"/>s
    /// that provide evidence for, or mention, this factual relationship.
    /// This links the extracted fact back to its source content.
    /// </summary>
    [JsonPropertyName("episodeIds")]
    public List<string>? EpisodeIds { get; init; }
}
