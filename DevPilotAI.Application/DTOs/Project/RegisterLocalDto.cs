using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Application.DTOs.Project;

public class RegisterLocalDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string SourceLocation { get; set; } = string.Empty;
}
