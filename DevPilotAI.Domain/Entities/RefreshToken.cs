using DevPilotAI.Domain.Common;
using DevPilotAI.Domain.Entities.Identity;

namespace DevPilotAI.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public string Token { get; set; } = string.Empty;
    public DateTime Expires { get; set; }
    public DateTime Created { get; set; }
    public string? CreatedByIp { get; set; }

    public DateTime? Revoked { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? ReasonRevoked { get; set; }

    public bool IsExpired => DateTime.UtcNow >= Expires;
    public bool IsRevoked => Revoked != null;
    public bool IsActive => !IsRevoked && !IsExpired;
}
