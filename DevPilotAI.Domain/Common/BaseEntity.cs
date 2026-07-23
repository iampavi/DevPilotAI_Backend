using System.ComponentModel.DataAnnotations.Schema;

namespace DevPilotAI.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [NotMapped]
    public List<DomainEvent> DomainEvents { get; } = new();
}
