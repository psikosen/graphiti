using GraphitiNet.Abstractions;
using GraphitiNet.Configuration; // For GraphitiNetOptions
using GraphitiNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options; // For IOptions
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GraphitiNet;

public class GraphitiClient : IGraphitiClient
{
    private readonly IGraphDatabaseService _graphDbService;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<GraphitiClient> _logger;
    private readonly GraphitiNetOptions _options; // To access default model names, index names

    // Constants for Neo4j vector index names and embedding property keys
    // These could also come from GraphitiNetOptions if more flexibility is needed.
    private const string DefaultNodeIndexName = "entityNodeNameEmbeddings"; // Example name
    private const string DefaultNodeEmbeddingPropertyKey = "nameEmbedding"; // Must match node property

    private const string DefaultEdgeIndexName = "entityEdgeFactEmbeddings"; // Example name
    private const string DefaultEdgeEmbeddingPropertyKey = "factEmbedding"; // Must match edge property


    public GraphitiClient(
        IGraphDatabaseService graphDbService,
        IEmbeddingGenerator embeddingGenerator,
        IOptions<GraphitiNetOptions> options, // Inject options
        ILogger<GraphitiClient> logger)
    {
        _graphDbService = graphDbService ?? throw new ArgumentNullException(nameof(graphDbService));
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EpisodicNode> IngestTextAsync(
        string content,
        string? source = null,
        DateTime? referenceTime = null,
        Dictionary<string, object>? properties = null,
        string? id = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be null or whitespace.", nameof(content));
        }

        var nodeId = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString() : id;
        var effectiveReferenceTime = referenceTime ?? DateTime.UtcNow;

        var episodicNode = new EpisodicNode
        {
            Id = nodeId,
            Content = content,
            Source = source,
            ReferenceTime = effectiveReferenceTime,
            Properties = properties ?? new Dictionary<string, object>()
        };

        try
        {
            // Assuming EpisodicNode model has ReferenceTime defined (from Task 3.4)
            // And AddOrUpdateEpisodicNodeAsync is defined in IGraphDatabaseService
            await _graphDbService.AddOrUpdateEpisodicNodeAsync(episodicNode);
            _logger.LogInformation("Successfully ingested text and stored EpisodicNode with ID: {NodeId}", episodicNode.Id);
            return episodicNode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing EpisodicNode with ID: {NodeId} during ingestion.", episodicNode.Id);
            throw;
        }
    }

    public async Task<List<EntityNodeSearchResult>> SearchSimilarNodesAsync(
        string queryText,
        int limit = 10,
        string? targetLabel = null) // targetLabel not used in DB query for MVP, can be post-filter
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            _logger.LogWarning("SearchSimilarNodesAsync called with empty query text.");
            return new List<EntityNodeSearchResult>();
        }
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }

        _logger.LogInformation("Searching for {Limit} similar nodes with query: '{QueryText}'", limit, queryText);

        float[]? queryVector;
        try
        {
            queryVector = await _embeddingGenerator.GenerateEmbeddingAsync(queryText, _options.DefaultEmbeddingModelName);
            if (queryVector == null || queryVector.Length == 0)
            {
                _logger.LogWarning("Failed to generate embedding for query text: '{QueryText}'. Search cannot proceed.", queryText);
                return new List<EntityNodeSearchResult>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding for query text: '{QueryText}'. Search failed.", queryText);
            throw; // Or return empty list depending on desired error handling
        }

        IEnumerable<(EntityNode Node, float Score)> dbResults;
        try
        {
            // Using constants for index name and property key for MVP. These could be made configurable via _options.
            dbResults = await _graphDbService.SearchNodesByVectorAsync(
                DefaultNodeIndexName,
                DefaultNodeEmbeddingPropertyKey, // This parameter might be removed from DB service if index implies property
                queryVector,
                limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching nodes by vector in the database. Query: '{QueryText}'", queryText);
            throw;
        }

        var searchResults = dbResults
            .Select(r => new EntityNodeSearchResult { Node = r.Node, Score = r.Score })
            .ToList();

        // MVP: targetLabel filtering can be done client-side if necessary,
        // though less efficient than DB-side. For true MVP, this might be omitted.
        if (!string.IsNullOrWhiteSpace(targetLabel))
        {
            searchResults = searchResults
                .Where(sr => sr.Node.Labels.Contains(targetLabel, StringComparer.OrdinalIgnoreCase))
                .ToList();
            _logger.LogDebug("Filtered node search results by targetLabel: '{TargetLabel}'. Count after filtering: {Count}", targetLabel, searchResults.Count);
        }
        
        _logger.LogInformation("Found {Count} similar nodes for query: '{QueryText}'", searchResults.Count, queryText);
        return searchResults;
    }

    public async Task<List<EntityEdgeSearchResult>> SearchSimilarEdgesAsync(
        string queryText,
        int limit = 10,
        string? targetRelationshipType = null) // targetRelationshipType not used in DB query for MVP
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            _logger.LogWarning("SearchSimilarEdgesAsync called with empty query text.");
            return new List<EntityEdgeSearchResult>();
        }
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }

        _logger.LogInformation("Searching for {Limit} similar edges with query: '{QueryText}'", limit, queryText);

        float[]? queryVector;
        try
        {
            queryVector = await _embeddingGenerator.GenerateEmbeddingAsync(queryText, _options.DefaultEmbeddingModelName);
            if (queryVector == null || queryVector.Length == 0)
            {
                _logger.LogWarning("Failed to generate embedding for query text: '{QueryText}'. Edge search cannot proceed.", queryText);
                return new List<EntityEdgeSearchResult>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding for query text: '{QueryText}'. Edge search failed.", queryText);
            throw;
        }

        IEnumerable<(EntityEdge Edge, float Score)> dbResults;
        try
        {
            // Using constants for index name and property key for MVP.
            dbResults = await _graphDbService.SearchEdgesByVectorAsync(
                DefaultEdgeIndexName,
                DefaultEdgeEmbeddingPropertyKey, // This parameter might be removed from DB service
                queryVector,
                limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching edges by vector in the database. Query: '{QueryText}'", queryText);
            throw;
        }

        var searchResults = dbResults
            .Select(r => new EntityEdgeSearchResult { Edge = r.Edge, Score = r.Score })
            .ToList();

        // MVP: targetRelationshipType filtering client-side
        if (!string.IsNullOrWhiteSpace(targetRelationshipType))
        {
            searchResults = searchResults
                .Where(sr => string.Equals(sr.Edge.RelationshipType, targetRelationshipType, StringComparison.OrdinalIgnoreCase))
                .ToList();
            _logger.LogDebug("Filtered edge search results by targetRelationshipType: '{TargetType}'. Count after filtering: {Count}", targetRelationshipType, searchResults.Count);
        }

        _logger.LogInformation("Found {Count} similar edges for query: '{QueryText}'", searchResults.Count, queryText);
        return searchResults;
    }
}
```
