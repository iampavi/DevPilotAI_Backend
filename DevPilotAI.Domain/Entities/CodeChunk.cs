using System;
using DevPilotAI.Domain.Common;

namespace DevPilotAI.Domain.Entities;

public class CodeChunk : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid ParsedFileId { get; set; }
    public ParsedFile ParsedFile { get; set; } = null!;

    public Guid? ParsedClassId { get; set; }
    public ParsedClass? ParsedClass { get; set; }

    public Guid? ParsedMethodId { get; set; }
    public ParsedMethod? ParsedMethod { get; set; }

    public string ChunkType { get; set; } = string.Empty; // Class, Method, Property, File
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public string Hash { get; set; } = string.Empty;
    
    public string EmbeddingModel { get; set; } = string.Empty;
    public int EmbeddingVersion { get; set; } = 1;

    // Serialized dictionary or layout string metadata
    public string Metadata { get; set; } = string.Empty;
}
