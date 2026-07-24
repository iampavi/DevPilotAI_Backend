using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Project;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Infrastructure.Services;

public class SemanticSearchService : ISemanticSearchService
{
    private readonly IQdrantService _qdrantService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public SemanticSearchService(
        IQdrantService qdrantService,
        IEmbeddingService embeddingService,
        IApplicationDbContext context,
        IMapper mapper)
    {
        _qdrantService = qdrantService;
        _embeddingService = embeddingService;
        _context = context;
        _mapper = mapper;
    }

    public async Task<List<CodeChunkDto>> SearchChunksAsync(Guid projectId, string query, int limit = 5, CancellationToken cancellationToken = default)
    {
        var queryVector = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        var collectionName = "devpilot-project-chunks";
        var matchedChunkIds = await _qdrantService.SearchSimilarityAsync(collectionName, queryVector, projectId, limit, cancellationToken);

        if (matchedChunkIds.Count == 0)
        {
            return new List<CodeChunkDto>();
        }

        var chunks = await _context.CodeChunks
            .Where(c => matchedChunkIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        // Maintain the rank ordering returned from Qdrant
        var sortedChunks = matchedChunkIds
            .Select(id => chunks.FirstOrDefault(c => c.Id == id))
            .Where(c => c != null)
            .ToList();

        return _mapper.Map<List<CodeChunkDto>>(sortedChunks);
    }
}
