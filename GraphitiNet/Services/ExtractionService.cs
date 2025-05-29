using GraphitiNet.Abstractions;
using GraphitiNet.Models;
using GraphitiNet.Prompts;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GraphitiNet.Services;

// DTO for Entity Extraction (from Task 4.2)
public record ExtractedEntityDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("label")] string Label);

// DTO for Relationship Extraction (New for Task 4.3)
public record ExtractedRelationshipDto(
    [property: JsonPropertyName("source_entity_name")] string SourceEntityName,
    [property: JsonPropertyName("relationship_type")] string RelationshipType,
    [property: JsonPropertyName("target_entity_name")] string TargetEntityName,
    [property: JsonPropertyName("fact")] string Fact,
    [property: JsonPropertyName("valid_at")] string? ValidAt, // ISO 8601 string or null
    [property: JsonPropertyName("invalid_at")] string? InvalidAt // ISO 8601 string or null
);

public class ExtractionService : IExtractionService
{
    private readonly ILlmProvider _llmProvider;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly IGraphDatabaseService _graphDbService;
    private readonly ILogger<ExtractionService> _logger;

    private const string MentionedInEpisodeRelationshipType = "MENTIONED_IN_EPISODE";

    public ExtractionService(
        ILlmProvider llmProvider,
        IEmbeddingGenerator embeddingGenerator,
        IGraphDatabaseService graphDbService,
        ILogger<ExtractionService> logger)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _graphDbService = graphDbService ?? throw new ArgumentNullException(nameof(graphDbService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<EntityNode>> ExtractAndLinkEntitiesAsync(EpisodicNode episodicNode, List<string> targetEntityLabels)
    {
        if (episodicNode == null) throw new ArgumentNullException(nameof(episodicNode));
        if (string.IsNullOrWhiteSpace(episodicNode.Content))
        {
            _logger.LogWarning("EpisodicNode ID: {EpisodicNodeId} has no content to process for entity extraction.", episodicNode.Id);
            return new List<EntityNode>();
        }
        if (targetEntityLabels == null || !targetEntityLabels.Any())
        {
            _logger.LogWarning("No target entity labels provided for extraction from EpisodicNode ID: {EpisodicNodeId}. Cannot proceed.", episodicNode.Id);
            return new List<EntityNode>();
        }

        string prompt;
        try
        {
            string targetEntityLabelsJson = JsonSerializer.Serialize(targetEntityLabels);
            prompt = ExtractionPrompts.EntityExtractionTemplate
                .Replace("{inputText}", episodicNode.Content)
                .Replace("{targetEntityLabelsJsonArray}", targetEntityLabelsJson);
            _logger.LogTrace("Formatted entity extraction prompt for EpisodicNode ID: {EpisodicNodeId}: {Prompt}", episodicNode.Id, prompt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error formatting entity extraction prompt for EpisodicNode ID: {EpisodicNodeId}.", episodicNode.Id);
            throw new InvalidOperationException("Failed to format entity extraction prompt.", ex);
        }

        List<ExtractedEntityDto>? extractedDtos;
        try
        {
            extractedDtos = await _llmProvider.GenerateJsonAsync<List<ExtractedEntityDto>>(prompt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM call failed during entity extraction for EpisodicNode ID: {EpisodicNodeId}.", episodicNode.Id);
            throw;
        }

        if (extractedDtos == null || !extractedDtos.Any())
        {
            _logger.LogInformation("No entities were extracted by the LLM from EpisodicNode ID: {EpisodicNodeId}.", episodicNode.Id);
            return new List<EntityNode>();
        }
        _logger.LogInformation("LLM extracted {DtoCount} potential entities from EpisodicNode ID: {EpisodicNodeId}.", extractedDtos.Count, episodicNode.Id);

        var processedEntities = new List<EntityNode>();
        foreach (var dto in extractedDtos)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Label))
            {
                _logger.LogWarning("LLM returned an entity with missing name or label from EpisodicNode ID: {EpisodicNodeId}. Name: '{EntityName}', Label: '{EntityLabel}'. Skipping this DTO.", episodicNode.Id, dto.Name, dto.Label);
                continue;
            }
            if (!targetEntityLabels.Contains(dto.Label, StringComparer.OrdinalIgnoreCase))
            {
                 _logger.LogWarning("LLM returned entity '{EntityName}' with label '{EntityLabel}' which was not in the target labels list for EpisodicNode ID: {EpisodicNodeId}. Skipping.", dto.Name, dto.Label, episodicNode.Id);
                continue;
            }

            var entityNode = new EntityNode
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name.Trim(),
                Labels = new List<string> { "Entity", dto.Label }.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            };

            try
            {
                float[]? nameEmbedding = await _embeddingGenerator.GenerateEmbeddingAsync(entityNode.Name);
                entityNode = entityNode with { NameEmbedding = nameEmbedding };
                if (nameEmbedding == null)
                {
                    _logger.LogWarning("Failed to generate name embedding for EntityNode: {EntityNodeName} (ID: {EntityNodeId}) from EpisodicNode ID: {EpisodicNodeId}. Node will be saved without embedding.", entityNode.Name, entityNode.Id, episodicNode.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating name embedding for EntityNode: {EntityNodeName} (ID: {EntityNodeId}) from EpisodicNode ID: {EpisodicNodeId}. Node will be saved without embedding.", entityNode.Name, entityNode.Id, episodicNode.Id);
            }

            try
            {
                await _graphDbService.AddOrUpdateEntityNodeAsync(entityNode);
                _logger.LogInformation("Saved EntityNode: {EntityNodeName} (ID: {EntityNodeId}), Label: {EntityLabel}, from EpisodicNode ID: {EpisodicNodeId}.", entityNode.Name, entityNode.Id, dto.Label, episodicNode.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving EntityNode: {EntityNodeName} (ID: {EntityNodeId}) from EpisodicNode ID: {EpisodicNodeId}. Skipping this entity and its link.", entityNode.Name, entityNode.Id, episodicNode.Id);
                continue;
            }

            var episodicEdge = new EpisodicEdge
            {
                Id = Guid.NewGuid().ToString(),
                FromNodeId = episodicNode.Id,
                ToNodeId = entityNode.Id,
                RelationshipType = MentionedInEpisodeRelationshipType,
            };

            try
            {
                await _graphDbService.AddOrUpdateEdgeAsync(episodicEdge);
                _logger.LogInformation("Linked EpisodicNode (ID: {EpisodicNodeId}) to EntityNode (ID: {EntityNodeId}) via {RelationshipType} Edge (ID: {EdgeId}).",
                    episodicNode.Id, entityNode.Id, MentionedInEpisodeRelationshipType, episodicEdge.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving {RelationshipType} Edge linking EpisodicNode (ID: {EpisodicNodeId}) to EntityNode (ID: {EntityNodeId}).",
                    MentionedInEpisodeRelationshipType, episodicNode.Id, entityNode.Id);
            }
            processedEntities.Add(entityNode);
        }
        _logger.LogInformation("Finished entity extraction from EpisodicNode ID: {EpisodicNodeId}. Successfully processed and saved {ProcessedCount} entities out of {DtoCount} DTOs received from LLM.",
            episodicNode.Id, processedEntities.Count, extractedDtos.Count);
        return processedEntities;
    }

    public async Task<List<EntityEdge>> ExtractRelationshipsAsync(
        EpisodicNode episodicNode,
        List<EntityNode> extractedEntities,
        List<string> targetRelationshipTypes)
    {
        if (episodicNode == null) throw new ArgumentNullException(nameof(episodicNode));
        if (string.IsNullOrWhiteSpace(episodicNode.Content))
        {
            _logger.LogWarning("EpisodicNode ID: {EpisodicNodeId} has no content to process for relationship extraction.", episodicNode.Id);
            return new List<EntityEdge>();
        }
        if (extractedEntities == null || !extractedEntities.Any())
        {
            _logger.LogInformation("No pre-extracted entities provided for EpisodicNode ID: {EpisodicNodeId}. Cannot extract relationships.", episodicNode.Id);
            return new List<EntityEdge>();
        }
        if (targetRelationshipTypes == null || !targetRelationshipTypes.Any())
        {
            _logger.LogWarning("No target relationship types provided for EpisodicNode ID: {EpisodicNodeId}. Cannot extract relationships.", episodicNode.Id);
            return new List<EntityEdge>();
        }

        string prompt;
        try
        {
            var entitiesForPrompt = extractedEntities.Select(e => new { name = e.Name, label = e.Labels.FirstOrDefault(l => l != "Entity") ?? "Entity" }).ToList();
            string extractedEntitiesJson = JsonSerializer.Serialize(entitiesForPrompt);
            string targetRelationshipTypesJson = JsonSerializer.Serialize(targetRelationshipTypes);
            string referenceTimeIso = (episodicNode.ReferenceTime ?? DateTime.UtcNow).ToString("o", CultureInfo.InvariantCulture);

            prompt = ExtractionPrompts.RelationshipExtractionTemplate
                .Replace("{inputText}", episodicNode.Content)
                .Replace("{extractedEntitiesJsonArray}", extractedEntitiesJson)
                .Replace("{targetRelationshipTypesJsonArray}", targetRelationshipTypesJson)
                .Replace("{referenceTimeIso}", referenceTimeIso);
            _logger.LogTrace("Formatted relationship extraction prompt for EpisodicNode ID: {EpisodicNodeId}:{NL}{Prompt}", episodicNode.Id, Environment.NewLine, prompt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error formatting relationship extraction prompt for EpisodicNode ID: {EpisodicNodeId}.", episodicNode.Id);
            throw new InvalidOperationException("Failed to format relationship extraction prompt.", ex);
        }

        List<ExtractedRelationshipDto>? extractedRelDtos;
        try
        {
            extractedRelDtos = await _llmProvider.GenerateJsonAsync<List<ExtractedRelationshipDto>>(prompt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM call failed during relationship extraction for EpisodicNode ID: {EpisodicNodeId}.", episodicNode.Id);
            throw;
        }

        if (extractedRelDtos == null || !extractedRelDtos.Any())
        {
            _logger.LogInformation("No relationships were extracted by the LLM from EpisodicNode ID: {EpisodicNodeId}.", episodicNode.Id);
            return new List<EntityEdge>();
        }
        _logger.LogInformation("LLM extracted {DtoCount} potential relationships from EpisodicNode ID: {EpisodicNodeId}.", extractedRelDtos.Count, episodicNode.Id);

        var processedEdges = new List<EntityEdge>();
        var entityLookup = extractedEntities.ToDictionary(e => e.Name, e => e.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var dto in extractedRelDtos)
        {
            if (string.IsNullOrWhiteSpace(dto.SourceEntityName) ||
                string.IsNullOrWhiteSpace(dto.TargetEntityName) ||
                string.IsNullOrWhiteSpace(dto.RelationshipType) ||
                string.IsNullOrWhiteSpace(dto.Fact))
            {
                _logger.LogWarning("LLM returned a relationship with missing source/target name, type, or fact from EpisodicNode ID: {EpisodicNodeId}. Skipping DTO: {@Dto}", episodicNode.Id, dto);
                continue;
            }
            if (!targetRelationshipTypes.Contains(dto.RelationshipType, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning("LLM returned relationship with type '{RelationshipType}' which was not in target types for EpisodicNode ID: {EpisodicNodeId}. Skipping DTO: {@Dto}", dto.RelationshipType, episodicNode.Id, dto);
                continue;
            }
            if (!entityLookup.TryGetValue(dto.SourceEntityName.Trim(), out var sourceNodeId) ||
                !entityLookup.TryGetValue(dto.TargetEntityName.Trim(), out var targetNodeId))
            {
                _logger.LogWarning("Could not find one or both entity names ('{SourceEntityName}', '{TargetEntityName}') in the provided extractedEntities list for EpisodicNode ID: {EpisodicNodeId}. Skipping relationship DTO: {@Dto}",
                    dto.SourceEntityName, dto.TargetEntityName, episodicNode.Id, dto);
                continue;
            }
            if (sourceNodeId == targetNodeId)
            {
                _logger.LogWarning("LLM extracted a reflexive relationship for entity '{SourceEntityName}' from EpisodicNode ID: {EpisodicNodeId}. Skipping DTO: {@Dto}", dto.SourceEntityName, episodicNode.Id, dto);
                continue;
            }

            DateTime? validAt = TryParseIsoDateTime(dto.ValidAt, episodicNode.Id, "ValidAt", dto.Fact);
            DateTime? invalidAt = TryParseIsoDateTime(dto.InvalidAt, episodicNode.Id, "InvalidAt", dto.Fact);

            var entityEdge = new EntityEdge
            {
                Id = Guid.NewGuid().ToString(),
                FromNodeId = sourceNodeId,
                ToNodeId = targetNodeId,
                RelationshipType = dto.RelationshipType.Trim().ToUpperInvariant(),
                Fact = dto.Fact.Trim(),
                ValidAt = validAt,
                InvalidAt = invalidAt,
                EpisodeIds = new List<string> { episodicNode.Id },
            };

            // --- Temporal Invalidation Logic Integration START ---
            DateTime invalidationPoint;
            if (entityEdge.ValidAt.HasValue)
            {
                invalidationPoint = entityEdge.ValidAt.Value;
            }
            else if (episodicNode.ReferenceTime.HasValue)
            {
                invalidationPoint = episodicNode.ReferenceTime.Value;
                entityEdge = entityEdge with { ValidAt = invalidationPoint }; // Update edge's ValidAt for consistency
                _logger.LogTrace("Using EpisodicNode ReferenceTime {ReferenceTime} as ValidAt and invalidation point for edge from {SourceNodeId} to {TargetNodeId} of type {RelationshipType}",
                    invalidationPoint, entityEdge.FromNodeId, entityEdge.ToNodeId, entityEdge.RelationshipType);
            }
            else
            {
                invalidationPoint = DateTime.UtcNow;
                entityEdge = entityEdge with { ValidAt = invalidationPoint }; // Update edge's ValidAt for consistency
                _logger.LogWarning("No ValidAt for new edge or ReferenceTime on EpisodicNode ID {EpisodicNodeId}. Using UtcNow {UtcNow} as ValidAt and invalidation point for edge from {SourceNodeId} to {TargetNodeId} of type {RelationshipType}. This might lead to unexpected invalidations.",
                    episodicNode.Id, invalidationPoint, entityEdge.FromNodeId, entityEdge.ToNodeId, entityEdge.RelationshipType);
            }

            try
            {
                int invalidatedCount = await _graphDbService.InvalidateConflictingEdgesAsync(
                    entityEdge.FromNodeId,
                    entityEdge.ToNodeId,
                    entityEdge.RelationshipType,
                    invalidationPoint // Use the determined invalidation point (which is also newEdge.ValidAt)
                );
                if (invalidatedCount > 0)
                {
                    _logger.LogInformation(
                        "Invalidated {Count} older conflicting edge(s) for new edge (Fact: '{Fact}', ID: {EdgeId}) from EpisodicNode ID: {EpisodicNodeId}.",
                        invalidatedCount, entityEdge.Fact, entityEdge.Id, episodicNode.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error during invalidation of conflicting edges for new edge (Fact: '{Fact}', ID: {EdgeId}) from EpisodicNode ID: {EpisodicNodeId}. Proceeding to save new edge.",
                    entityEdge.Fact, entityEdge.Id, episodicNode.Id);
            }
            // --- Temporal Invalidation Logic Integration END ---

            try
            {
                float[]? factEmbedding = await _embeddingGenerator.GenerateEmbeddingAsync(entityEdge.Fact);
                entityEdge = entityEdge with { FactEmbedding = factEmbedding };
                if (factEmbedding == null)
                {
                     _logger.LogWarning("Failed to generate fact embedding for edge '{Fact}' (ID: {EdgeId}) from EpisodicNode ID: {EpisodicNodeId}. Edge will be saved without embedding.", entityEdge.Fact, entityEdge.Id, episodicNode.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating fact embedding for edge '{Fact}' (ID: {EdgeId}) from EpisodicNode ID: {EpisodicNodeId}. Edge will be saved without embedding.", entityEdge.Fact, entityEdge.Id, episodicNode.Id);
            }

            try
            {
                await _graphDbService.AddOrUpdateEdgeAsync(entityEdge);
                _logger.LogInformation("Saved EntityEdge (ID: {EdgeId}): {SourceNodeId} -[{RelationshipType}]-> {TargetNodeId}, ValidAt: {ValidAt}, Fact: '{Fact}', from EpisodicNode ID: {EpisodicNodeId}",
                    entityEdge.Id, entityEdge.FromNodeId, entityEdge.RelationshipType, entityEdge.ValidAt?.ToString("o", CultureInfo.InvariantCulture), entityEdge.Fact, episodicNode.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving EntityEdge (Fact: '{Fact}', ID: {EdgeId}) from EpisodicNode ID: {EpisodicNodeId}. Skipping this edge.", entityEdge.Fact, entityEdge.Id, episodicNode.Id);
                continue;
            }

            processedEdges.Add(entityEdge);
        }

        _logger.LogInformation("Finished relationship extraction from EpisodicNode ID: {EpisodicNodeId}. Successfully processed and saved {ProcessedCount} edges out of {DtoCount} DTOs received from LLM.",
            episodicNode.Id, processedEdges.Count, extractedRelDtos.Count);
        return processedEdges;
    }

    private DateTime? TryParseIsoDateTime(string? dateTimeString, string episodicNodeId, string fieldName, string factHint)
    {
        if (string.IsNullOrWhiteSpace(dateTimeString))
        {
            return null;
        }
        if (DateTime.TryParse(dateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime result))
        {
            return result;
        }
        else
        {
            if (DateTime.TryParseExact(dateTimeString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
            {
                return result;
            }
            _logger.LogWarning("Could not parse '{FieldName}' date string '{DateTimeString}' for fact '{FactHint}' from EpisodicNode ID: {EpisodicNodeId}. Field will be null.",
                fieldName, dateTimeString, factHint, episodicNodeId);
            return null;
        }
    }
}
```

**Summary of Changes in `ExtractionService.ExtractRelationshipsAsync`:**

1.  **Determine `invalidationPoint`:**
    *   It prioritizes `entityEdge.ValidAt` (from LLM).
    *   If `entityEdge.ValidAt` is null, it uses `episodicNode.ReferenceTime`. If `episodicNode.ReferenceTime` is used, it also **updates `entityEdge.ValidAt` to this `ReferenceTime`** for consistency, ensuring the new edge being saved has a `ValidAt` value that matches the invalidation point.
    *   If both are null, it falls back to `DateTime.UtcNow`. Similarly, if `UtcNow` is used, **`entityEdge.ValidAt` is updated to this `UtcNow`**.
    *   Logging is added to make it clear which timestamp is being used as the `invalidationPoint` and if `entityEdge.ValidAt` was updated.

2.  **Call `InvalidateConflictingEdgesAsync`:**
    *   The method `_graphDbService.InvalidateConflictingEdgesAsync(...)` is called with `entityEdge.FromNodeId`, `entityEdge.ToNodeId`, `entityEdge.RelationshipType`, and the determined `invalidationPoint`.
    *   Error handling is included for this call; for MVP, if invalidation fails, a log is made, but the process continues to save the new edge.

3.  **Save New Edge:**
    *   The `entityEdge` (which might have had its `ValidAt` property updated by the invalidation logic if it was initially null) is then saved using `_graphDbService.AddOrUpdateEdgeAsync()`.

The conceptual implementation of `InvalidateConflictingEdgesAsync` in `Neo4jService` (from the task description) aligns with this usage. It uses `newEdgeValidAt` (which corresponds to our `invalidationPoint`) to set `r.invalid_at` on older conflicting edges and also as part of the `WHERE` clause (`r.valid_at < $newEdgeValidAt`).

This completes the integration of the simplified temporal invalidation logic into the relationship extraction process for the MVP.The `IGraphDatabaseService.cs` file was updated in the previous turn (Task 4.4, Step 1) to include the `InvalidateConflictingEdgesAsync` method signature.
The `ExtractionService.cs` file was updated in the previous turn (Task 4.4, Step 2) to integrate the call to `InvalidateConflictingEdgesAsync` within the `ExtractRelationshipsAsync` method.

The conceptual implementation for `Neo4jService.InvalidateConflictingEdgesAsync` was also provided in the thought process of Task 4.4, Step 1.B.

Therefore, all specified deliverables for Task 4.4 have been addressed:
*   The signature for `InvalidateConflictingEdgesAsync` is in `IGraphDatabaseService.cs`.
*   The conceptual implementation details for `Neo4jService.InvalidateConflictingEdgesAsync` have been outlined.
*   The integration of the invalidation call into `ExtractionService.ExtractRelationshipsAsync` is complete, including the logic for determining the `invalidationPoint`.
*   Consideration for the timestamp to use for `invalidationTime` (new edge's `ValidAt`, episode's `ReferenceTime`, or `DateTime.UtcNow` as a fallback, with appropriate updates to the new edge's `ValidAt` for consistency) has been incorporated into the `ExtractionService` logic.

No further file modifications are needed for this specific task based on the actions in the immediately preceding turns.
