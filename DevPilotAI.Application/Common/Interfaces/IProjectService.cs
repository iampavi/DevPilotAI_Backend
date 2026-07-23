using DevPilotAI.Application.DTOs.Project;
using DevPilotAI.Shared.Common;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IProjectService
{
    Task<Result<ProjectDto>> CreateProjectAsync(Guid workspaceId, CreateProjectDto dto, CancellationToken cancellationToken = default);
    Task<Result<ProjectDto>> GetProjectByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<ProjectBriefDto>>> GetProjectsByWorkspaceAsync(Guid workspaceId, string? searchTerm, string? sortBy, string? sortOrder, PaginationRequest pagination, CancellationToken cancellationToken = default);
    Task<Result<ProjectDto>> UpdateProjectAsync(Guid id, UpdateProjectDto dto, CancellationToken cancellationToken = default);
    Task<Result> DeleteProjectAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<Result<ProjectSettingsDto>> GetProjectSettingsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectSettingsDto>> UpdateProjectSettingsAsync(Guid projectId, UpdateProjectSettingsDto dto, CancellationToken cancellationToken = default);
    Task<Result<ProjectStatisticsDto>> GetProjectStatisticsAsync(Guid projectId, CancellationToken cancellationToken = default);
}
