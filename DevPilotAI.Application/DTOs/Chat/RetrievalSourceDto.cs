using System;

namespace DevPilotAI.Application.DTOs.Chat;

public class RetrievalSourceDto
{
    public Guid ChunkId { get; set; }
    public string FilePath { get; set; } = null!;
    public string SymbolName { get; set; } = null!;
    public double SimilarityScore { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
}
