using System.Collections.Generic;
using DevPilotAI.Application.DTOs.Project;

namespace DevPilotAI.Application.DTOs.Chat;

public class RepositoryContextDto
{
    public List<CodeChunkDto> SeedChunks { get; set; } = new();
    public List<CodeChunkDto> ExpandedChunks { get; set; } = new();
    public List<RepositoryRelationshipDto> Relationships { get; set; } = new();
    public List<string> ReferencedSymbols { get; set; } = new();
    public List<string> RelatedFiles { get; set; } = new();
    public int ExpansionDepth { get; set; }
    public int ExpandedSymbolCount { get; set; }
}
