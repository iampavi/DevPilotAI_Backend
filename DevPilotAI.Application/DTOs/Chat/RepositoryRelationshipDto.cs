namespace DevPilotAI.Application.DTOs.Chat;

public class RepositoryRelationshipDto
{
    public string FromSymbol { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty; // implements, inherits, injects, calls, uses, referenced_by
    public string ToSymbol { get; set; } = string.Empty;
}
