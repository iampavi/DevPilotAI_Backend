using DevPilotAI.Domain.Common;
using DevPilotAI.Domain.Entities;

namespace DevPilotAI.Domain.Events;

public class WorkspaceCreatedEvent : DomainEvent
{
    public WorkspaceCreatedEvent(Workspace workspace)
    {
        Workspace = workspace;
    }

    public Workspace Workspace { get; }
}
