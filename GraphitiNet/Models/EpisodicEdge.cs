using System.Text.Json.Serialization;

namespace GraphitiNet.Models;

/// <summary>
/// Represents a "MENTIONS" or "DERIVED_FROM_EPISODE" relationship,
/// connecting an <see cref="EpisodicNode"/> (source) to an <see cref="EntityNode"/> (target)
/// that was mentioned or identified within that episode.
/// </summary>
public record EpisodicEdge : BaseEdge
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodicEdge"/> class.
    /// The <see cref="BaseEdge.RelationshipType"/> is typically set by the service creating this edge.
    /// </summary>
    public EpisodicEdge()
    {
        // While RelationshipType is required in BaseEdge,
        // it's often set dynamically when the edge is created.
        // Example: RelationshipType = "MENTIONED_IN_EPISODE";
    }

    // No additional properties are defined for EpisodicEdge in the MVP beyond those in BaseEdge.
    // Custom properties can be added to the `Properties` dictionary if needed.
    // For example, one might store the specific sentence or span from the EpisodicNode content
    // that led to the creation of this link, but that's beyond MVP.
}
```
