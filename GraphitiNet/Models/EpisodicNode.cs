using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GraphitiNet.Models;

/// <summary>
/// Represents a piece of ingested content or data that acts as a source or evidence
/// for information within the knowledge graph. For the MVP, it's simplified to hold
/// the content and an optional source identifier.
/// </summary>
public record EpisodicNode : BaseNode
{
    /// <summary>
    /// The raw text content or data of the episode.
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>
    /// An optional identifier for the source of this information
    /// (e.g., a document URI, a message ID, a system name).
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodicNode"/> class.
    /// Automatically adds "Episodic" as the primary label.
    /// </summary>
    public EpisodicNode()
    {
        // Ensure "Episodic" is the primary label.
        Labels = new List<string> { "Episodic" };
    }
}
