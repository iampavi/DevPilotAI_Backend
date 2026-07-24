using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace DevPilotAI.Infrastructure.Services;

public class QdrantService : IQdrantService
{
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantService> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    public QdrantService(IConfiguration configuration, ILogger<QdrantService> logger)
    {
        _logger = logger;
        
        var host = configuration["QdrantSettings:Host"] ?? "localhost";
        var portStr = configuration["QdrantSettings:Port"] ?? "6334";
        var apiKey = configuration["QdrantSettings:ApiKey"];
        var useHttps = bool.TryParse(configuration["QdrantSettings:Https"], out var https) && https;

        int port = int.TryParse(portStr, out var p) ? p : 6334;

        _logger.LogInformation("Connecting to Qdrant at {Host}:{Port} (Https: {Https})", host, port, useHttps);

        if (!string.IsNullOrEmpty(apiKey))
        {
            _client = new QdrantClient(host, port, https: useHttps, apiKey: apiKey);
        }
        else
        {
            _client = new QdrantClient(host, port, https: useHttps);
        }

        // Configure Polly retry pipeline (retry 3 times with exponential backoff)
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    logger.LogWarning(args.Outcome.Exception, "Qdrant operation failed. Retrying {Attempt}...", args.AttemptNumber);
                    return default;
                }
            })
            .Build();
    }

    public async Task EnsureCollectionExistsAsync(string collectionName, int dimensions, CancellationToken cancellationToken = default)
    {
        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var collections = await _client.ListCollectionsAsync(token);
            if (!collections.Contains(collectionName))
            {
                _logger.LogInformation("Creating Qdrant collection {CollectionName} with dimension {Dimensions}", collectionName, dimensions);
                await _client.CreateCollectionAsync(
                    collectionName: collectionName,
                    vectorsConfig: new VectorParams { Size = (ulong)dimensions, Distance = Distance.Cosine },
                    cancellationToken: token);
            }
        }, cancellationToken);
    }

    public async Task UpsertVectorsAsync(string collectionName, List<QdrantPointDto> points, CancellationToken cancellationToken = default)
    {
        if (points == null || points.Count == 0) return;

        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var qdrantPoints = points.Select(p =>
            {
                var point = new PointStruct
                {
                    Id = p.ChunkId,
                    Vectors = p.Vector
                };
                
                point.Payload["project_id"] = p.ProjectId.ToString();
                point.Payload["file_id"] = p.FileId.ToString();
                point.Payload["file_path"] = p.FilePath;
                point.Payload["symbol_name"] = p.SymbolName;
                point.Payload["chunk_type"] = p.ChunkType;

                return point;
            }).ToList();

            _logger.LogInformation("Upserting {Count} vectors into collection {CollectionName}", qdrantPoints.Count, collectionName);
            await _client.UpsertAsync(collectionName, qdrantPoints, cancellationToken: token);
        }, cancellationToken);
    }

    public async Task DeleteVectorsByProjectAsync(string collectionName, Guid projectId, CancellationToken cancellationToken = default)
    {
        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            _logger.LogInformation("Deleting vectors for project {ProjectId} in collection {CollectionName}", projectId, collectionName);
            
            var filter = new Filter
            {
                Must = { Conditions.MatchKeyword("project_id", projectId.ToString()) }
            };

            await _client.DeleteAsync(collectionName, filter, cancellationToken: token);
        }, cancellationToken);
    }

    public async Task DeleteVectorsAsync(string collectionName, List<Guid> chunkIds, CancellationToken cancellationToken = default)
    {
        if (chunkIds == null || chunkIds.Count == 0) return;

        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            _logger.LogInformation("Deleting {Count} specific vectors from collection {CollectionName}", chunkIds.Count, collectionName);
            await _client.DeleteAsync(collectionName, chunkIds, cancellationToken: token);
        }, cancellationToken);
    }

    public async Task<List<Guid>> SearchSimilarityAsync(string collectionName, float[] queryVector, Guid projectId, int limit = 5, CancellationToken cancellationToken = default)
    {
        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var filter = new Filter
            {
                Must = { Conditions.MatchKeyword("project_id", projectId.ToString()) }
            };

            var searchResult = await _client.SearchAsync(
                collectionName: collectionName,
                vector: queryVector,
                filter: filter,
                limit: (ulong)limit,
                cancellationToken: token);

            return searchResult.Select(r => Guid.Parse(r.Id.Uuid)).ToList();
        }, cancellationToken);
    }
}
