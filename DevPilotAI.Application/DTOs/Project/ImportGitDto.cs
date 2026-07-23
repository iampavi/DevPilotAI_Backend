using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Application.DTOs.Project;

public class ImportGitDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Url]
    [MaxLength(500)]
    public string RepositoryUrl { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Branch { get; set; } = "main";

    [MaxLength(200)]
    public string? PersonalAccessToken { get; set; }
}
