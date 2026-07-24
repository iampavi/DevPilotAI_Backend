using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DevPilotAI.Application.Common.Interfaces;

public record QdrantPointDto(
    Guid ChunkId,
    float[] Vector,
    Guid ProjectId,
    Guid FileId,
    string FilePath,
    string SymbolName,
    string ChunkType
);

public interface IQdrantService
{
    Task EnsureCollectionExistsAsync(string collectionName, int dimensions, CancellationToken cancellationToken = default);
    Task UpsertVectorsAsync(string collectionName, List<QdrantPointDto> points, CancellationToken cancellationToken = default);
    Task DeleteVectorsByProjectAsync(string collectionName, Guid projectId, CancellationToken cancellationToken = default);
    Task DeleteVectorsAsync(string collectionName, List<Guid> chunkIds, CancellationToken cancellationToken = default);
    
    // Low-level similarity search returning chunk IDs
    Task<List<Guid>> SearchSimilarityAsync(string collectionName, float[] queryVector, Guid projectId, int limit = 5, CancellationToken cancellationToken = default);
}
