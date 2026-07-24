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

    Task<Result<ProjectDto>> RegisterLocalProjectAsync(Guid workspaceId, RegisterLocalDto dto, CancellationToken cancellationToken = default);
    Task<Result<ProjectImportJobDto>> ImportZipProjectAsync(Guid workspaceId, string name, Stream zipStream, string fileName, CancellationToken cancellationToken = default);
    Task<Result<ProjectImportJobDto>> ImportGitProjectAsync(Guid workspaceId, ImportGitDto dto, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProjectImportJobDto>>> GetProjectImportJobsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectImportJobDto>> GetProjectImportJobByIdAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<Result<ProjectParseJobDto>> ParseProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProjectParseJobDto>>> GetProjectParseJobsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectParseJobDto>> GetProjectParseJobByIdAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<Result<ProjectChunkingJobDto>> ChunkProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ProjectChunkingJobDto>>> GetProjectChunkingJobsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<Result<ProjectChunkingJobDto>> GetProjectChunkingJobByIdAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<CodeChunkDto>>> GetProjectChunksAsync(Guid projectId, int pageNumber, int pageSize, string? chunkType = null, CancellationToken cancellationToken = default);
}
