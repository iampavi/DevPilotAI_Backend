using Microsoft.AspNetCore.Identity;

namespace DevPilotAI.Domain.Entities.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<Workspace> Workspaces { get; set; } = new List<Workspace>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
