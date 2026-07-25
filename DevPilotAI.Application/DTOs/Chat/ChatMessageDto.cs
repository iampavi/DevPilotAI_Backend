using System;

namespace DevPilotAI.Application.DTOs.Chat;

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public string Role { get; set; } = null!; // "system", "user", "assistant"
    public string Content { get; set; } = null!;
    public int TokenCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Metadata { get; set; } // JSON list of RetrievalSourceDto
}
