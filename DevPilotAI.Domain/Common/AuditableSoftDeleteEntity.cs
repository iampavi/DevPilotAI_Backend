namespace DevPilotAI.Domain.Common;

public abstract class AuditableSoftDeleteEntity : AuditableEntity, ISoftDelete
{
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
