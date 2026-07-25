using System;
using System.Collections.Generic;
using DevPilotAI.Domain.Common;

namespace DevPilotAI.Domain.Entities;

public class ChatSession : AuditableSoftDeleteEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Title { get; set; } = null!;
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
