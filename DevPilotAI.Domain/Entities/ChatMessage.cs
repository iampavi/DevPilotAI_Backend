using System;
using DevPilotAI.Domain.Common;

namespace DevPilotAI.Domain.Entities;

public class ChatMessage : BaseEntity
{
    public Guid ChatSessionId { get; set; }
    public ChatSession ChatSession { get; set; } = null!;
    public string Role { get; set; } = null!; // "system", "user", "assistant"
    public string Content { get; set; } = null!;
    public int TokenCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Metadata { get; set; } // Stores serialized JSON string of RetrievalSourceDto[]
}
