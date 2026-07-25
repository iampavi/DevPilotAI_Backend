using System;
using System.Collections.Generic;

namespace DevPilotAI.Application.DTOs.Chat;

public class ChatSessionDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
}
