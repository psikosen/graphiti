using GraphitiNet.Models;
using System.Collections.Generic;
using System.Linq; // Required for Enumerable.Any() and FirstOrDefault()
using System.Text; // Required for StringBuilder
using System; // Required for DateTime formatting

namespace GraphitiNet.Helpers;

public static class ContextAssembler
{
    /// <summary>
    /// Assembles a textual context string from entity node and entity edge search results.
    /// For MVP, this method assembles all provided results without token-based truncation.
    /// </summary>
    /// <param name="nodeResults">An enumerable of entity node search results. Can be null or empty.</param>
    /// <param name="edgeResults">An enumerable of entity edge search results. Can be null or empty.</param>
    /// <param name="includeScores">Optional: Whether to include the similarity scores in the context string. Defaults to false.</param>
    /// <returns>A single string containing the formatted context from the search results.</returns>
    public static string AssembleContextFromSearchResults(
        IEnumerable<EntityNodeSearchResult>? nodeResults,
        IEnumerable<EntityEdgeSearchResult>? edgeResults,
        bool includeScores = false)
    {
        var contextBuilder = new StringBuilder();
        bool hasNodeResults = nodeResults?.Any() ?? false;
        bool hasEdgeResults = edgeResults?.Any() ?? false;

        if (!hasNodeResults && !hasEdgeResults)
        {
            return "No relevant information found.";
        }

        if (hasNodeResults)
        {
            contextBuilder.AppendLine("Relevant Entities:");
            int nodeCount = 1;
            foreach (var nodeResult in nodeResults!) // Null forgiven due to hasNodeResults check
            {
                var node = nodeResult.Node;
                string primaryLabel = node.Labels.FirstOrDefault(l => l != "Entity") ?? "Entity";
                contextBuilder.Append($"{nodeCount}. Entity: {node.Name} (Type: {primaryLabel})");
                if (includeScores)
                {
                    contextBuilder.Append($" (Score: {nodeResult.Score:F2})");
                }
                contextBuilder.AppendLine();

                if (!string.IsNullOrWhiteSpace(node.Summary))
                {
                    contextBuilder.AppendLine($"   Summary: {node.Summary}");
                }

                // Include some key properties if any, for MVP, let's list up to 3 non-empty/null properties
                // excluding known top-level properties already handled (Id, CreatedAt, Labels, Name, Summary, NameEmbedding)
                var relevantProperties = node.Properties
                    .Where(kvp => kvp.Value != null && !string.IsNullOrWhiteSpace(kvp.Value.ToString()))
                    .Take(3) // MVP: Limit to a few properties
                    .ToList();

                if (relevantProperties.Any())
                {
                    contextBuilder.AppendLine("   Key Properties:");
                    foreach (var prop in relevantProperties)
                    {
                        contextBuilder.AppendLine($"     - {prop.Key}: {prop.Value}");
                    }
                }
                nodeCount++;
            }
            contextBuilder.AppendLine(); // Add a blank line after entities section
        }

        if (hasEdgeResults)
        {
            contextBuilder.AppendLine("Relevant Facts/Relationships:");
            int edgeCount = 1;
            foreach (var edgeResult in edgeResults!) // Null forgiven due to hasEdgeResults check
            {
                var edge = edgeResult.Edge;
                contextBuilder.Append($"{edgeCount}. Fact: {edge.Fact}");
                if (includeScores)
                {
                    contextBuilder.Append($" (Score: {edgeResult.Score:F2})");
                }
                contextBuilder.AppendLine();

                // For MVP, we will NOT look up source/target node names here to avoid DB calls.
                // The Fact itself should ideally be self-contained enough or the LLM can infer from multiple facts.
                // We can state the relationship type and connected node IDs for more technical context if desired.
                contextBuilder.AppendLine($"   Type: {edge.RelationshipType}");
                contextBuilder.AppendLine($"   Connects: Node ID '{edge.FromNodeId}' to Node ID '{edge.ToNodeId}'");


                if (edge.ValidAt.HasValue)
                {
                    contextBuilder.AppendLine($"   Valid From: {edge.ValidAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
                }
                if (edge.InvalidAt.HasValue)
                {
                    contextBuilder.AppendLine($"   Valid Until: {edge.InvalidAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
                }
                edgeCount++;
            }
            contextBuilder.AppendLine(); // Add a blank line after facts section
        }

        return contextBuilder.ToString().Trim();
    }
}
```

**3. Example of Formatted Context String Output:**

Let's assume the following sample data:

*   **Node Results:**
    1.  `EntityNodeSearchResult`
        *   `Node`: `Id="node1", Name="Quantum Leap Inc.", Labels=["Entity", "Organization"], Summary="A leading AI research company.", Properties={"FoundedYear": 2010, "Headquarters": "San Francisco"}`
        *   `Score`: 0.85f
    2.  `EntityNodeSearchResult`
        *   `Node`: `Id="node2", Name="Dr. Eva Rostova", Labels=["Entity", "Person"], Summary="Chief Scientist at Quantum Leap Inc., specializing in temporal mechanics.", Properties={"Expertise": "Temporal Mechanics", "Affiliation": "Quantum Leap Inc."}`
        *   `Score`: 0.79f

*   **Edge Results:**
    1.  `EntityEdgeSearchResult`
        *   `Edge`: `Id="edge1", FromNodeId="node2", ToNodeId="node1", RelationshipType="WORKS_FOR", Fact="Dr. Eva Rostova is the Chief Scientist at Quantum Leap Inc.", ValidAt=new DateTime(2018, 5, 1, 0,0,0, DateTimeKind.Utc), InvalidAt=null`
        *   `Score`: 0.92f
    2.  `EntityEdgeSearchResult`
        *   `Edge`: `Id="edge2", FromNodeId="node1", ToNodeId="someProjectNode", RelationshipType="DEVELOPED", Fact="Quantum Leap Inc. developed the Chronos Project.", ValidAt=new DateTime(2022, 1, 10, 0,0,0, DateTimeKind.Utc), InvalidAt=new DateTime(2023,12,31,0,0,0, DateTimeKind.Utc)`
        *   `Score`: 0.88f

**Formatted Output (with `includeScores = true`):**

```text
Relevant Entities:
1. Entity: Quantum Leap Inc. (Type: Organization) (Score: 0.85)
   Summary: A leading AI research company.
   Key Properties:
     - FoundedYear: 2010
     - Headquarters: San Francisco
2. Entity: Dr. Eva Rostova (Type: Person) (Score: 0.79)
   Summary: Chief Scientist at Quantum Leap Inc., specializing in temporal mechanics.
   Key Properties:
     - Expertise: Temporal Mechanics
     - Affiliation: Quantum Leap Inc.

Relevant Facts/Relationships:
1. Fact: Dr. Eva Rostova is the Chief Scientist at Quantum Leap Inc. (Score: 0.92)
   Type: WORKS_FOR
   Connects: Node ID 'node2' to Node ID 'node1'
   Valid From: 2018-05-01 00:00:00 UTC
2. Fact: Quantum Leap Inc. developed the Chronos Project. (Score: 0.88)
   Type: DEVELOPED
   Connects: Node ID 'node1' to Node ID 'someProjectNode'
   Valid From: 2022-01-10 00:00:00 UTC
   Valid Until: 2023-12-31 00:00:00 UTC

```

**Formatted Output (with `includeScores = false`):**

```text
Relevant Entities:
1. Entity: Quantum Leap Inc. (Type: Organization)
   Summary: A leading AI research company.
   Key Properties:
     - FoundedYear: 2010
     - Headquarters: San Francisco
2. Entity: Dr. Eva Rostova (Type: Person)
   Summary: Chief Scientist at Quantum Leap Inc., specializing in temporal mechanics.
   Key Properties:
     - Expertise: Temporal Mechanics
     - Affiliation: Quantum Leap Inc.

Relevant Facts/Relationships:
1. Fact: Dr. Eva Rostova is the Chief Scientist at Quantum Leap Inc.
   Type: WORKS_FOR
   Connects: Node ID 'node2' to Node ID 'node1'
   Valid From: 2018-05-01 00:00:00 UTC
2. Fact: Quantum Leap Inc. developed the Chronos Project.
   Type: DEVELOPED
   Connects: Node ID 'node1' to Node ID 'someProjectNode'
   Valid From: 2022-01-10 00:00:00 UTC
   Valid Until: 2023-12-31 00:00:00 UTC

```

**4. Decision on Source/Target Node Information for Edges (MVP):**

*   **Decision:** For MVP, the `AssembleContextFromSearchResults` method will **NOT** perform additional database lookups to fetch source/target node names for edges.
*   **Reasoning:**
    *   **Simplicity:** Avoids adding a dependency on `IGraphDatabaseService` to this helper class and keeps its logic focused on formatting the data it receives.
    *   **Performance:** Prevents potential N+1 query issues if many edges are returned.
    *   **Sufficient for LLM (Potentially):** The `Fact` string in `EntityEdge` should ideally be descriptive enough. If multiple related facts and entities are retrieved, the LLM might be able to infer connections.
    *   **Contextual Clues:** The context string already includes node information if `nodeResults` are also provided. If the edge's `FromNodeId` or `ToNodeId` matches an ID of a node in `nodeResults`, the LLM can correlate them.
*   **What's Included for Edges:**
    *   The edge's `Fact`.
    *   The edge's `RelationshipType`.
    *   The `FromNodeId` and `ToNodeId` (raw IDs). This provides a way for an advanced user or the LLM (if capable) to understand connectivity at an ID level.
    *   `ValidAt` and `InvalidAt` timestamps.
*   **Future Enhancement:** Post-MVP, if more detailed edge context is needed, options could include:
    *   Modifying search results to optionally include source/target node names (if the graph query can efficiently fetch them).
    *   Allowing the `ContextAssembler` to optionally take an `IGraphDatabaseService` or a dictionary of already fetched nodes to enrich edge information.

This approach provides a simple, readable, and useful context string for the MVP, focusing on presenting the retrieved information without adding significant complexity for this step.
All deliverables for this task have been addressed.
