using GraphitiNet.Abstractions;
using GraphitiNet.Models;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json; // For serializing/deserializing the Properties dictionary if needed for complex types
using System.Threading.Tasks;

namespace GraphitiNet.Infrastructure.Neo4j;

/// <summary>
/// Service for interacting with a Neo4j database, implementing IGraphDatabaseService.
/// </summary>
public class Neo4jService : IGraphDatabaseService
{
    private readonly IDriver _driver;
    private readonly ILogger<Neo4jService> _logger;
    private readonly string _databaseName; // Allow configurable database name

    public Neo4jService(IDriver driver, ILogger<Neo4jService> logger, string? databaseName = null)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _databaseName = string.IsNullOrWhiteSpace(databaseName) ? "neo4j" : databaseName;
    }

    public async Task AddOrUpdateEntityNodeAsync(EntityNode node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));

        var query = @"
            MERGE (n:Entity {id: $id})
            ON CREATE SET n = $props, n.createdAt = $createdAt, n.name = $name, n.summary = $summary, n.nameEmbedding = $nameEmbedding
            ON MATCH SET n += $props, n.name = $name, n.summary = $summary, n.nameEmbedding = $nameEmbedding, n.updatedAt = timestamp()
            WITH n
            UNWIND $labels AS labelIn
            CALL apoc.create.addLabels(n, [labelIn]) YIELD node AS resultNode
            RETURN count(resultNode) > 0 AS success"; // Ensuring labels are applied

        var nodeProperties = ToNeo4jProperties(node.Properties);
        nodeProperties["id"] = node.Id; // Ensure id is part of the base properties for CREATE
        nodeProperties["createdAt"] = node.CreatedAt; // Store as Neo4j DateTime

        var parameters = new
        {
            id = node.Id,
            name = node.Name,
            summary = node.Summary,
            nameEmbedding = node.NameEmbedding,
            props = nodeProperties,
            createdAt = node.CreatedAt, // For ON CREATE
            labels = node.Labels.Distinct().ToArray() // Ensure "Entity" is present via model constructor
        };

        try
        {
            await ExecuteWriteAsync(query, parameters);
            _logger.LogInformation("Successfully added/updated EntityNode with ID: {NodeId}", node.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding/updating EntityNode with ID: {NodeId}", node.Id);
            throw new GraphDatabaseException($"Error processing EntityNode with ID: {node.Id}", ex);
        }
    }

    public async Task AddOrUpdateEpisodicNodeAsync(EpisodicNode node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));

        var query = @"
            MERGE (e:Episodic {id: $id})
            ON CREATE SET e = $props, e.createdAt = $createdAt, e.content = $content, e.source = $source
            ON MATCH SET e += $props, e.content = $content, e.source = $source, e.updatedAt = timestamp()
            WITH e
            UNWIND $labels AS labelIn
            CALL apoc.create.addLabels(e, [labelIn]) YIELD node AS resultNode
            RETURN count(resultNode) > 0 AS success";

        var nodeProperties = ToNeo4jProperties(node.Properties);
        nodeProperties["id"] = node.Id;
        nodeProperties["createdAt"] = node.CreatedAt;

        var parameters = new
        {
            id = node.Id,
            content = node.Content,
            source = node.Source,
            props = nodeProperties,
            createdAt = node.CreatedAt,
            labels = node.Labels.Distinct().ToArray() // Ensure "Episodic" is present
        };

        try
        {
            await ExecuteWriteAsync(query, parameters);
            _logger.LogInformation("Successfully added/updated EpisodicNode with ID: {NodeId}", node.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding/updating EpisodicNode with ID: {NodeId}", node.Id);
            throw new GraphDatabaseException($"Error processing EpisodicNode with ID: {node.Id}", ex);
        }
    }

    public async Task AddOrUpdateEntityEdgeAsync(EntityEdge edge)
    {
        if (edge == null) throw new ArgumentNullException(nameof(edge));

        // Using apoc.merge.relationship for more robust edge creation/update
        var query = $@"
            MATCH (from {{id: $fromNodeId}})
            MATCH (to {{id: $toNodeId}})
            CALL apoc.merge.relationship(from, '{SanitizeRelationshipType(edge.RelationshipType)}', {{id: $id}}, $props, to) YIELD rel
            SET rel.fact = $fact, rel.factEmbedding = $factEmbedding, 
                rel.validAt = $validAt, rel.invalidAt = $invalidAt, rel.episodeIds = $episodeIds,
                rel.createdAt = $createdAt 
            RETURN rel";
            // Removed ON MATCH SET rel.updatedAt = timestamp() as apoc.merge.relationship updates all properties

        var edgeProperties = ToNeo4jProperties(edge.Properties);
        // Explicitly set known properties, others go into $props for apoc.merge.relationship's initial merge
        // `id` is used in the identity properties for merge.
        // `createdAt` is set explicitly after merge to ensure it's there.

        var parameters = new
        {
            id = edge.Id,
            fromNodeId = edge.FromNodeId,
            toNodeId = edge.ToNodeId,
            props = edgeProperties, // Properties for apoc.merge.relationship to set on create/match
            fact = edge.Fact,
            factEmbedding = edge.FactEmbedding,
            validAt = edge.ValidAt, // Neo4j driver handles DateTime to Neo4j DateTime
            invalidAt = edge.InvalidAt,
            episodeIds = edge.EpisodeIds,
            createdAt = edge.CreatedAt
        };
        
        try
        {
            await ExecuteWriteAsync(query, parameters);
            _logger.LogInformation("Successfully added/updated EntityEdge with ID: {EdgeId} of type {RelationshipType}", edge.Id, edge.RelationshipType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding/updating EntityEdge with ID: {EdgeId} of type {RelationshipType}", edge.Id, edge.RelationshipType);
            throw new GraphDatabaseException($"Error processing EntityEdge with ID: {edge.Id} of type {edge.RelationshipType}", ex);
        }
    }

    public async Task<EntityNode?> GetEntityNodeAsync(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) throw new ArgumentException("Node ID cannot be null or whitespace.", nameof(nodeId));

        var query = "MATCH (n:Entity {id: $id}) RETURN n";
        var parameters = new { id = nodeId };

        try
        {
            return await ExecuteReadSingleAsync(query, parameters, record => MapToEntityNode(record["n"].As<INode>()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving EntityNode with ID: {NodeId}", nodeId);
            throw new GraphDatabaseException($"Error retrieving EntityNode with ID: {nodeId}", ex);
        }
    }

    public async Task<EntityEdge?> GetEntityEdgeAsync(string edgeId)
    {
        if (string.IsNullOrWhiteSpace(edgeId)) throw new ArgumentException("Edge ID cannot be null or whitespace.", nameof(edgeId));
        
        var query = "MATCH (from)-[r {id: $id}]->(to) RETURN r, from.id AS fromNodeId, to.id AS toNodeId";
        var parameters = new { id = edgeId };
        
        try
        {
            return await ExecuteReadSingleAsync(query, parameters, record =>
                MapToEntityEdge(record["r"].As<IRelationship>(), record["fromNodeId"].As<string>(), record["toNodeId"].As<string>()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving EntityEdge with ID: {EdgeId}", edgeId);
            throw new GraphDatabaseException($"Error retrieving EntityEdge with ID: {edgeId}", ex);
        }
    }
    
    public async Task<IEnumerable<EntityNode>> SearchNodesByVectorAsync(string indexName, string embeddingPropertyKey, float[] vector, int limit)
    {
        // embeddingPropertyKey is part of the index definition in Neo4j, not directly in the CALL
        var query = @"
            CALL db.index.vector.queryNodes($indexName, $k, $vector)
            YIELD node, score
            RETURN node";

        var parameters = new { indexName, k = limit, vector };
        
        try
        {
            return await ExecuteReadListAsync(query, parameters, record => MapToEntityNode(record["node"].As<INode>()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching nodes by vector in index: {IndexName}", indexName);
            throw new GraphDatabaseException($"Error searching nodes by vector in index: {indexName}", ex);
        }
    }

    public async Task<IEnumerable<EntityEdge>> SearchEdgesByVectorAsync(string indexName, string embeddingPropertyKey, float[] vector, int limit)
    {
        // embeddingPropertyKey is part of the index definition in Neo4j
        var query = @"
            CALL db.index.vector.queryRelationships($indexName, $k, $vector)
            YIELD relationship, score
            MATCH (from)-[relationship]->(to)
            RETURN relationship, from.id AS fromNodeId, to.id AS toNodeId";

        var parameters = new { indexName, k = limit, vector };

        try
        {
            return await ExecuteReadListAsync(query, parameters, record =>
                MapToEntityEdge(record["relationship"].As<IRelationship>(), record["fromNodeId"].As<string>(), record["toNodeId"].As<string>()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching edges by vector in index: {IndexName}", indexName);
            throw new GraphDatabaseException($"Error searching edges by vector in index: {indexName}", ex);
        }
    }

    public async Task<IEnumerable<EntityEdge>> GetConnectedEdgesAsync(string nodeId, EdgeDirection direction = EdgeDirection.Both, params string[]? relationshipTypes)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) throw new ArgumentException("Node ID cannot be null or whitespace.", nameof(nodeId));

        string typeFilter = (relationshipTypes != null && relationshipTypes.Any())
            ? ":" + string.Join("|", relationshipTypes.Select(SanitizeRelationshipType))
            : "";

        var query = direction switch
        {
            EdgeDirection.Outgoing => $"MATCH (n {{id: $nodeId}})-[r{typeFilter}]->(m) RETURN r, n.id as fromNodeId, m.id as toNodeId",
            EdgeDirection.Incoming => $"MATCH (m)-[r{typeFilter}]->(n {{id: $nodeId}}) RETURN r, m.id as fromNodeId, n.id as toNodeId",
            _ => $"MATCH (n {{id: $nodeId}})-[r{typeFilter}]-(m) RETURN r, startNode(r).id as fromNodeId, endNode(r).id as toNodeId",
        };
        var parameters = new { nodeId };

        try
        {
            return await ExecuteReadListAsync(query, parameters, record =>
                MapToEntityEdge(record["r"].As<IRelationship>(), record["fromNodeId"].As<string>(), record["toNodeId"].As<string>()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving connected edges for node ID: {NodeId}", nodeId);
            throw new GraphDatabaseException($"Error retrieving connected edges for node ID: {nodeId}", ex);
        }
    }

    private async Task ExecuteWriteAsync(string query, object? parameters = null)
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_databaseName));
        await session.ExecuteWriteAsync(async tx => await tx.RunAsync(query, parameters));
    }

    private async Task<T?> ExecuteReadSingleAsync<T>(string query, object? parameters, Func<IRecord, T> mapper) where T : class
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_databaseName));
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(query, parameters);
            return await cursor.FetchAsync() ? mapper(cursor.Current) : null;
        });
        return result;
    }

    private async Task<List<T>> ExecuteReadListAsync<T>(string query, object? parameters, Func<IRecord, T> mapper)
    {
        await using var session = _driver.AsyncSession(o => o.WithDatabase(_databaseName));
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(query, parameters);
            return await cursor.ToListAsync(mapper);
        });
        return result;
    }

    private static EntityNode MapToEntityNode(INode dbNode)
    {
        var props = new Dictionary<string, object>(dbNode.Properties);
        return new EntityNode
        {
            Id = GetAndRemove<string>(props, "id")!,
            Labels = new List<string>(dbNode.Labels), // Labels are part of INode, not properties map
            CreatedAt = GetAndRemoveDateTime(props, "createdAt"),
            Name = GetAndRemove<string>(props, "name")!,
            Summary = GetAndRemove<string>(props, "summary"),
            NameEmbedding = GetAndRemove<float[]>(props, "nameEmbedding"),
            Properties = props // Remaining properties
        };
    }
    
    private static EpisodicNode MapToEpisodicNode(INode dbNode)
    {
        var props = new Dictionary<string, object>(dbNode.Properties);
        return new EpisodicNode
        {
            Id = GetAndRemove<string>(props, "id")!,
            Labels = new List<string>(dbNode.Labels),
            CreatedAt = GetAndRemoveDateTime(props, "createdAt"),
            Content = GetAndRemove<string>(props, "content")!,
            Source = GetAndRemove<string>(props, "source"),
            Properties = props
        };
    }
    
    private static EntityEdge MapToEntityEdge(IRelationship dbRel, string fromNodeId, string toNodeId)
    {
        var props = new Dictionary<string, object>(dbRel.Properties);
        return new EntityEdge
        {
            Id = GetAndRemove<string>(props, "id")!,
            RelationshipType = dbRel.Type,
            FromNodeId = fromNodeId,
            ToNodeId = toNodeId,
            CreatedAt = GetAndRemoveDateTime(props, "createdAt"),
            Fact = GetAndRemove<string>(props, "fact")!,
            FactEmbedding = GetAndRemove<float[]>(props, "factEmbedding"),
            ValidAt = GetAndRemoveDateTimeNullable(props, "validAt"),
            InvalidAt = GetAndRemoveDateTimeNullable(props, "invalidAt"),
            EpisodeIds = GetAndRemove<List<string>>(props, "episodeIds"),
            Properties = props
        };
    }

    private static T? GetAndRemove<T>(IDictionary<string, object> properties, string key)
    {
        if (properties.TryGetValue(key, out var value))
        {
            properties.Remove(key);
            if (value is T typedValue) return typedValue;
            if (value is JsonElement jsonElement && typeof(T) != typeof(JsonElement)) // Handle potential JsonElement from deserialization
            {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
            }
            // Attempt conversion for common cases, e.g., int64 to int if T is int
            try { return (T)Convert.ChangeType(value, typeof(T)); }
            catch { /* Conversion failed, return default */ }
        }
        return default;
    }
    
    private static DateTime GetAndRemoveDateTime(IDictionary<string, object> properties, string key)
    {
        var obj = GetAndRemove<object>(properties, key);
        if (obj is ZonedDateTime zdt) return zdt.ToDateTimeOffset().UtcDateTime;
        if (obj is LocalDateTime ldt) return ldt.ToDateTime(); // Potential timezone ambiguity if not stored as ZonedDateTime
        if (obj is DateTime dt) return DateTime.SpecifyKind(dt, DateTimeKind.Utc); // Assume UTC if kind is Unspecified
        if (obj is long timestamp) return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime; // If stored as timestamp
        return DateTime.MinValue; // Or throw, indicates missing or incompatible type
    }

    private static DateTime? GetAndRemoveDateTimeNullable(IDictionary<string, object> properties, string key)
    {
        var obj = GetAndRemove<object>(properties, key);
        if (obj == null) return null;
        if (obj is ZonedDateTime zdt) return zdt.ToDateTimeOffset().UtcDateTime;
        if (obj is LocalDateTime ldt) return ldt.ToDateTime();
        if (obj is DateTime dt) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        if (obj is long timestamp) return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
        return null;
    }

    private static Dictionary<string, object> ToNeo4jProperties(IReadOnlyDictionary<string, object> properties)
    {
        // Convert DateTime to Neo4j compatible ZonedDateTime or ensure UTC for driver conversion
        return properties.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value is DateTime dt
                ? (object)dt.ToUniversalTime() // Driver handles DateTime to Neo4j DateTime
                : kvp.Value
        );
    }

    private static string SanitizeRelationshipType(string relationshipType)
    {
        if (string.IsNullOrWhiteSpace(relationshipType))
            throw new ArgumentException("Relationship type cannot be null or empty.", nameof(relationshipType));
        // Replace invalid characters or use backticks for safety if type contains special chars.
        // For MVP, we'll assume types are reasonably clean or use backticks.
        // A more robust sanitizer would check against Neo4j naming rules.
        // Neo4j allows most UTF-8 characters if backticked. If not backticked, must be alphanumeric + underscore.
        if (relationshipType.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
        {
             // Basic check: if it's not simple alphanumeric_underscore, quote it.
             // This doesn't escape internal backticks within the name itself.
            return $"`{relationshipType.Replace("`", "``")}`";
        }
        return relationshipType;
    }
}

// Moved GraphDatabaseException to a separate file if it's to be used across the project,
// or keep it here if specific to Neo4jService. For now, keeping it simple.
// public class GraphDatabaseException : Exception ... (defined in IGraphDatabaseService.cs or a shared Exceptions file)
```
