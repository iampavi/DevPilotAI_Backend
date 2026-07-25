using System;

namespace DevPilotAI.Application.DTOs.Project;

public class CodeChunkDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ParsedFileId { get; set; }
    public Guid? ParsedClassId { get; set; }
    public Guid? ParsedMethodId { get; set; }
    public string ChunkType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public int EmbeddingVersion { get; set; }
    public string Metadata { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string RetrievalExplanation { get; set; } = string.Empty;
}
