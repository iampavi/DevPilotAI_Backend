using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Project;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.Services;

public class SemanticRetrievalService : ISemanticRetrievalService
{
    private readonly IQdrantService _qdrantService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<SemanticRetrievalService> _logger;

    private readonly int _topK;
    private readonly double _similarityThreshold;
    private readonly int _maxContextChunks;

    public SemanticRetrievalService(
        IQdrantService qdrantService,
        IEmbeddingService embeddingService,
        IApplicationDbContext context,
        IConfiguration configuration,
        ILogger<SemanticRetrievalService> logger)
    {
        _qdrantService = qdrantService;
        _embeddingService = embeddingService;
        _context = context;
        _logger = logger;

        _topK = int.TryParse(configuration["RagSettings:TopK"], out var k) ? k : 5;
        _similarityThreshold = double.TryParse(configuration["RagSettings:SimilarityThreshold"], out var threshold) ? threshold : 0.7;
        _maxContextChunks = int.TryParse(configuration["RagSettings:MaxContextChunks"], out var max) ? max : 10;
    }

    public async Task<List<CodeChunkDto>> RetrieveRelevantContextAsync(Guid projectId, string query, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting semantic retrieval for project {ProjectId} with query: {Query}", projectId, query);

        // 1. Generate Query Embedding
        var queryVector = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
        if (queryVector == null || queryVector.Length == 0)
        {
            _logger.LogWarning("Failed to generate query embedding.");
            return new List<CodeChunkDto>();
        }

        // 2. Query Qdrant
        var collectionName = "devpilot-project-chunks";
        var qdrantResults = await _qdrantService.SearchSimilarityAsync(collectionName, queryVector, projectId, _topK, cancellationToken);
        
        if (qdrantResults == null || qdrantResults.Count == 0)
        {
            _logger.LogInformation("No similarity matches found in Qdrant.");
            return new List<CodeChunkDto>();
        }

        // 3. Resolve from SQL database
        var chunks = await _context.CodeChunks
            .Where(c => c.ProjectId == projectId && qdrantResults.Contains(c.Id))
            .ToListAsync(cancellationToken);

        // Map mock similarity score based on order/rank: Rank 0 gets 0.95, Rank 1 gets 0.90, etc.
        var scoresMap = new Dictionary<Guid, double>();
        for (int i = 0; i < qdrantResults.Count; i++)
        {
            scoresMap[qdrantResults[i]] = Math.Max(0.5, 0.95 - (i * 0.05));
        }

        // Apply similarity threshold filter
        var filteredChunks = chunks
            .Where(c => scoresMap.TryGetValue(c.Id, out var score) && score >= _similarityThreshold)
            .ToList();

        _logger.LogInformation("Found {Count} matches above threshold {Threshold}", filteredChunks.Count, _similarityThreshold);

        if (filteredChunks.Count == 0)
        {
            return new List<CodeChunkDto>();
        }

        // 4. TF-IDF/Keyword Lexical Re-ranking
        var queryKeywords = query
            .ToLowerInvariant()
            .Split(new[] { ' ', '.', '_', ':', '(', ')', '{', '}', '[', ']', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .Distinct()
            .ToList();

        var rankedList = new List<(CodeChunk Chunk, double Score)>();

        foreach (var chunk in filteredChunks)
        {
            var similarityScore = scoresMap.TryGetValue(chunk.Id, out var s) ? s : 0.0;
            var contentLower = chunk.Content.ToLowerInvariant();

            double lexicalScore = 0.0;
            if (queryKeywords.Count > 0)
            {
                var matches = queryKeywords.Count(kw => contentLower.Contains(kw));
                lexicalScore = (double)matches / queryKeywords.Count;
            }

            var finalScore = similarityScore + 0.15 * lexicalScore;
            rankedList.Add((chunk, finalScore));
        }

        var classIds = filteredChunks.Where(c => c.ParsedClassId.HasValue).Select(c => c.ParsedClassId!.Value).Distinct().ToList();
        var methodIds = filteredChunks.Where(c => c.ParsedMethodId.HasValue).Select(c => c.ParsedMethodId!.Value).Distinct().ToList();

        var classes = classIds.Any()
            ? await _context.ParsedClasses.Where(c => classIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken)
            : new Dictionary<Guid, ParsedClass>();

        var methods = methodIds.Any()
            ? await _context.ParsedMethods.Where(m => methodIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, cancellationToken)
            : new Dictionary<Guid, ParsedMethod>();

        var finalOrderedChunks = new List<CodeChunkDto>();
        foreach (var r in rankedList.OrderByDescending(x => x.Score).Take(_maxContextChunks))
        {
            var chunk = r.Chunk;
            int startLine = 1;
            int endLine = 1;

            if (chunk.ParsedMethodId.HasValue && methods.TryGetValue(chunk.ParsedMethodId.Value, out var method))
            {
                startLine = method.StartLine;
                endLine = method.EndLine;
            }
            else if (chunk.ParsedClassId.HasValue && classes.TryGetValue(chunk.ParsedClassId.Value, out var cls))
            {
                startLine = cls.StartLine;
                endLine = cls.EndLine;
            }
            else
            {
                var lineMatch = System.Text.RegularExpressions.Regex.Match(chunk.Content, @"Lines (\d+)-(\d+)");
                if (lineMatch.Success)
                {
                    startLine = int.Parse(lineMatch.Groups[1].Value);
                    endLine = int.Parse(lineMatch.Groups[2].Value);
                }
                else
                {
                    var linesCount = chunk.Content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Length;
                    startLine = 1;
                    endLine = Math.Max(1, linesCount);
                }
            }

            finalOrderedChunks.Add(new CodeChunkDto
            {
                Id = chunk.Id,
                ProjectId = chunk.ProjectId,
                ParsedFileId = chunk.ParsedFileId,
                ParsedClassId = chunk.ParsedClassId,
                ParsedMethodId = chunk.ParsedMethodId,
                ChunkType = chunk.ChunkType,
                Content = chunk.Content,
                TokenCount = chunk.TokenCount,
                Hash = chunk.Hash,
                EmbeddingModel = chunk.EmbeddingModel,
                EmbeddingVersion = chunk.EmbeddingVersion,
                Metadata = chunk.Metadata,
                StartLine = startLine,
                EndLine = endLine
            });
        }

        _logger.LogInformation("Retrieved and ranked {Count} context chunks.", finalOrderedChunks.Count);
        return finalOrderedChunks;
    }
}
