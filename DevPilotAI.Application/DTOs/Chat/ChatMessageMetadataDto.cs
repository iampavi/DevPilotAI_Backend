using System.Collections.Generic;

namespace DevPilotAI.Application.DTOs.Chat;

public class ChatMessageMetadataDto
{
    public List<RetrievalSourceDto> Sources { get; set; } = new();
    public double? ConfidenceScore { get; set; }
    public List<string>? RetrievedSymbols { get; set; }
    public List<string>? SourceFiles { get; set; }
    public int? RetrievedChunksCount { get; set; }
    public double? SimilarityThreshold { get; set; }
    public int? ChunkCount { get; set; }
    public string? ModelUsed { get; set; }
    public string? Provider { get; set; }
    public double? ResponseTimeMs { get; set; }
    public string? PromptMode { get; set; }
    public List<RepositoryRelationshipDto>? Relationships { get; set; } = new();
}
