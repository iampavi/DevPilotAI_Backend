using DevPilotAI.Domain.Common;
using DevPilotAI.Domain.Enums;

namespace DevPilotAI.Domain.Entities;

public class ProjectIndex : AuditableSoftDeleteEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    
    public string IndexVersion { get; set; } = "v1.0";
    public IndexStatus IndexStatus { get; set; } = IndexStatus.Unindexed;
    public DateTime? LastIndexedAt { get; set; }
    public string? EmbeddingModel { get; set; }
    public int ChunkCount { get; set; }
    public int EmbeddingCount { get; set; }
    public string? ParserVersion { get; set; }
}
