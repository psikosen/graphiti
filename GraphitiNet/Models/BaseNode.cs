using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GraphitiNet.Models;

/// <summary>
/// Abstract base record for all node types in GraphitiNet.
/// Provides common properties shared by all nodes.
/// </summary>
public abstract record BaseNode
{
    /// <summary>
    /// Unique identifier for the node. Typically a UUID string.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Timestamp (UTC) indicating when the node was created in the GraphitiNet system.
    /// Defaults to the time of object instantiation.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// A list of labels associated with the node. In graph databases like Neo4j,
    /// labels are used to categorize nodes. The first label in this list
    /// is often considered the primary type of the node.
    /// </summary>
    [JsonPropertyName("labels")]
    public required List<string> Labels { get; init; } = new List<string>();

    /// <summary>
    /// A dictionary for storing additional, dynamic properties associated with the node.
    /// This allows for schema flexibility beyond the explicitly defined properties.
    /// Property values can be simple types (string, number, boolean) or nested objects/arrays.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; init; } = new Dictionary<string, object>();
}
