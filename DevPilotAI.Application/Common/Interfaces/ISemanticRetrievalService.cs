using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.DTOs.Project;

namespace DevPilotAI.Application.Common.Interfaces;

public class DetailedRetrievalResult
{
    public List<CodeChunkDto> Chunks { get; set; } = [];
    public int CandidateChunks { get; set; }
    public int FilteredChunks { get; set; }
    public int FinalChunks { get; set; }
    public List<string> IgnoredReasons { get; set; } = [];
    public double AverageSimilarity { get; set; }
    public TimeSpan RetrievalTime { get; set; }
}

public interface ISemanticRetrievalService
{
    Task<List<CodeChunkDto>> RetrieveRelevantContextAsync(Guid projectId, string query, CancellationToken cancellationToken = default);
    Task<DetailedRetrievalResult> RetrieveDetailedContextAsync(Guid projectId, string query, CancellationToken cancellationToken = default);
}
