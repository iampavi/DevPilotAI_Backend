using DevPilotAI.Domain.Common;
using DevPilotAI.Domain.Entities;

namespace DevPilotAI.Domain.Events;

public class ProjectCreatedEvent : DomainEvent
{
    public ProjectCreatedEvent(Project project)
    {
        Project = project;
    }

    public Project Project { get; }
}
