using DevPilotAI.Application.DTOs.Workspace;
using DevPilotAI.Shared.Common;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IWorkspaceService
{
    Task<Result<WorkspaceDto>> CreateWorkspaceAsync(CreateWorkspaceDto dto, CancellationToken cancellationToken = default);
    Task<Result<WorkspaceDto>> GetWorkspaceByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<WorkspaceBriefDto>>> GetWorkspacesAsync(string? searchTerm, string? sortBy, string? sortOrder, PaginationRequest pagination, CancellationToken cancellationToken = default);
    Task<Result<WorkspaceDto>> UpdateWorkspaceAsync(Guid id, UpdateWorkspaceDto dto, CancellationToken cancellationToken = default);
    Task<Result> DeleteWorkspaceAsync(Guid id, CancellationToken cancellationToken = default);
}
