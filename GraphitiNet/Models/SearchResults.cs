using System.Text.Json.Serialization;

    namespace GraphitiNet.Models;

    /// <summary>
    /// Represents a search result for an EntityNode, including the node and its similarity score.
    /// </summary>
    public record EntityNodeSearchResult
    {
        /// <summary>
        /// The entity node found.
        /// </summary>
        [JsonPropertyName("node")]
        public required EntityNode Node { get; init; }

        /// <summary>
        /// The similarity score of this node to the search query.
        /// Higher scores typically indicate greater similarity. The range depends on the vector DB.
        /// </summary>
        [JsonPropertyName("score")]
        public required float Score { get; init; }
    }

    /// <summary>
    /// Represents a search result for an EntityEdge, including the edge and its similarity score.
    /// </summary>
    public record EntityEdgeSearchResult
    {
        /// <summary>
        /// The entity edge found.
        /// </summary>
        [JsonPropertyName("edge")]
        public required EntityEdge Edge { get; init; }

        /// <summary>
        /// The similarity score of this edge to the search query.
        /// Higher scores typically indicate greater similarity. The range depends on the vector DB.
        /// </summary>
        [JsonPropertyName("score")]
        public required float Score { get; init; }
    }
    ```
