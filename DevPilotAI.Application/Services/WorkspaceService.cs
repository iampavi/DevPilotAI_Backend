using AutoMapper;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Workspace;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Events;
using DevPilotAI.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Application.Services;

public class WorkspaceService : IWorkspaceService
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<WorkspaceService> _logger;

    public WorkspaceService(
        IApplicationDbContext context,
        IMapper mapper,
        ICurrentUserService currentUserService,
        ILogger<WorkspaceService> logger)
    {
        _context = context;
        _mapper = mapper;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<WorkspaceDto>> CreateWorkspaceAsync(CreateWorkspaceDto dto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to create workspace with name: {Name}", dto.Name);

        // Resolve current user ID
        var systemUserId = Guid.Parse("D035B9FE-B7FE-438B-B0D1-1C349C3AF21F");
        var userId = Guid.TryParse(_currentUserService.UserId, out var parsedId) ? parsedId : systemUserId;

        var isDuplicate = await _context.Workspaces
            .AnyAsync(w => w.UserId == userId && w.Name == dto.Name, cancellationToken);

        if (isDuplicate)
        {
            _logger.LogWarning("Failed to create workspace. Name {Name} is already taken for user {UserId}.", dto.Name, userId);
            return Result.Failure<WorkspaceDto>(new Error("Workspace.DuplicateName", "Workspace name is already in use by this user."));
        }

        var workspace = _mapper.Map<Workspace>(dto);
        workspace.UserId = userId;

        // Add domain event (prepared for downstream execution)
        workspace.DomainEvents.Add(new WorkspaceCreatedEvent(workspace));

        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Workspace created successfully with ID: {WorkspaceId} for user {UserId}", workspace.Id, userId);
        
        var workspaceDto = _mapper.Map<WorkspaceDto>(workspace);
        return Result.Success(workspaceDto);
    }

    public async Task<Result<WorkspaceDto>> GetWorkspaceByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (workspace == null)
        {
            _logger.LogWarning("Workspace not found for ID: {WorkspaceId}", id);
            return Result.Failure<WorkspaceDto>(new Error("Workspace.NotFound", $"Workspace with ID '{id}' was not found."));
        }

        var dto = _mapper.Map<WorkspaceDto>(workspace);
        return Result.Success(dto);
    }

    public async Task<Result<PagedResult<WorkspaceBriefDto>>> GetWorkspacesAsync(
        string? searchTerm, 
        string? sortBy, 
        string? sortOrder, 
        PaginationRequest pagination, 
        CancellationToken cancellationToken = default)
    {
        var query = _context.Workspaces.AsNoTracking();

        // Searching
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(w => w.Name.Contains(searchTerm) || (w.Description != null && w.Description.Contains(searchTerm)));
        }

        // Sorting
        bool isDesc = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        query = sortBy?.ToLower() switch
        {
            "name" => isDesc ? query.OrderByDescending(w => w.Name) : query.OrderBy(w => w.Name),
            "description" => isDesc ? query.OrderByDescending(w => w.Description) : query.OrderBy(w => w.Description),
            "createdat" => isDesc ? query.OrderByDescending(w => w.CreatedAt) : query.OrderBy(w => w.CreatedAt),
            _ => query.OrderByDescending(w => w.CreatedAt) // Default sorting
        };

        // Pagination
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var briefs = _mapper.Map<IReadOnlyList<WorkspaceBriefDto>>(items);
        var result = PagedResult<WorkspaceBriefDto>.Create(briefs, pagination.PageNumber, pagination.PageSize, totalCount);

        return Result.Success(result);
    }

    public async Task<Result<WorkspaceDto>> UpdateWorkspaceAsync(Guid id, UpdateWorkspaceDto dto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to update workspace: {WorkspaceId}", id);

        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (workspace == null)
        {
            _logger.LogWarning("Failed to update workspace. ID {WorkspaceId} not found.", id);
            return Result.Failure<WorkspaceDto>(new Error("Workspace.NotFound", $"Workspace with ID '{id}' was not found."));
        }

        var isDuplicate = await _context.Workspaces
            .AnyAsync(w => w.UserId == workspace.UserId && w.Name == dto.Name && w.Id != id, cancellationToken);

        if (isDuplicate)
        {
            _logger.LogWarning("Failed to update workspace {WorkspaceId}. Name {Name} is already taken by another workspace for this user.", id, dto.Name);
            return Result.Failure<WorkspaceDto>(new Error("Workspace.DuplicateName", "Workspace name is already in use by another workspace for this user."));
        }

        // Apply changes
        _mapper.Map(dto, workspace);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Workspace updated successfully: {WorkspaceId}", id);

        var resultDto = _mapper.Map<WorkspaceDto>(workspace);
        return Result.Success(resultDto);
    }

    public async Task<Result> DeleteWorkspaceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to delete workspace: {WorkspaceId}", id);

        var workspace = await _context.Workspaces
            .Include(w => w.Projects) // Include projects so soft-delete cascade flows correctly
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (workspace == null)
        {
            _logger.LogWarning("Failed to delete workspace. ID {WorkspaceId} not found.", id);
            return Result.Failure(new Error("Workspace.NotFound", $"Workspace with ID '{id}' was not found."));
        }

        // Deleting workspace will trigger soft-delete interception on workspaces and included projects
        _context.Workspaces.Remove(workspace);
        
        // Also soft-delete nested projects explicitly to ensure the interceptor triggers for them
        foreach (var project in workspace.Projects)
        {
            _context.Projects.Remove(project);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Workspace deleted successfully: {WorkspaceId}", id);
        return Result.Success();
    }
}
