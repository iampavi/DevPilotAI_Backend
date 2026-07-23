namespace DevPilotAI.Application.DTOs.Identity;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
