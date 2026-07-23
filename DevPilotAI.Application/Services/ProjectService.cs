using AutoMapper;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Project;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using DevPilotAI.Domain.Events;
using DevPilotAI.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Application.Services;

public class ProjectService : IProjectService
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(
        IApplicationDbContext context,
        IMapper mapper,
        ILogger<ProjectService> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<ProjectDto>> CreateProjectAsync(Guid workspaceId, CreateProjectDto dto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to create project with name: {Name} in workspace: {WorkspaceId}", dto.Name, workspaceId);

        var workspaceExists = await _context.Workspaces
            .AnyAsync(w => w.Id == workspaceId, cancellationToken);

        if (!workspaceExists)
        {
            _logger.LogWarning("Failed to create project. Workspace ID {WorkspaceId} not found.", workspaceId);
            return Result.Failure<ProjectDto>(new Error("Workspace.NotFound", $"Workspace with ID '{workspaceId}' was not found."));
        }

        var isDuplicate = await _context.Projects
            .AnyAsync(p => p.WorkspaceId == workspaceId && p.Name == dto.Name, cancellationToken);

        if (isDuplicate)
        {
            _logger.LogWarning("Failed to create project. Name {Name} is already taken in workspace {WorkspaceId}.", dto.Name, workspaceId);
            return Result.Failure<ProjectDto>(new Error("Project.DuplicateName", "Project name is already in use in this workspace."));
        }

        var project = _mapper.Map<Project>(dto);
        project.WorkspaceId = workspaceId;

        // Initialize nested structures
        project.Settings = new ProjectSettings
        {
            ExcludedFolders = new List<string> { "bin", "obj", "node_modules", ".git", ".vs", "dist", "out" },
            ExcludedExtensions = new List<string> { ".png", ".jpg", ".jpeg", ".gif", ".zip", ".pdb", ".exe", ".dll", ".mp3", ".mp4", ".pdf", ".docx" },
            MaxFileSizeInBytes = 5242880 // 5MB
        };

        project.Statistics = new ProjectStatistics
        {
            FileCount = 0,
            TotalLinesOfCode = 0,
            TotalBytes = 0,
            IndexedFileCount = 0,
            ControllerCount = 0,
            ServiceCount = 0,
            RepositoryCount = 0,
            ApiCount = 0,
            ClassCount = 0
        };

        project.Index = new ProjectIndex
        {
            IndexVersion = "v1.0",
            IndexStatus = IndexStatus.Unindexed,
            ChunkCount = 0,
            EmbeddingCount = 0
        };

        // Add domain event (prepared for downstream execution)
        project.DomainEvents.Add(new ProjectCreatedEvent(project));

        _context.Projects.Add(project);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Project created successfully with ID: {ProjectId} in workspace: {WorkspaceId}", project.Id, workspaceId);

        var resultDto = _mapper.Map<ProjectDto>(project);
        return Result.Success(resultDto);
    }

    public async Task<Result<ProjectDto>> GetProjectByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects
            .Include(p => p.Settings)
            .Include(p => p.Statistics)
            .Include(p => p.Index)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (project == null)
        {
            _logger.LogWarning("Project not found for ID: {ProjectId}", id);
            return Result.Failure<ProjectDto>(new Error("Project.NotFound", $"Project with ID '{id}' was not found."));
        }

        var dto = _mapper.Map<ProjectDto>(project);
        return Result.Success(dto);
    }

    public async Task<Result<PagedResult<ProjectBriefDto>>> GetProjectsByWorkspaceAsync(
        Guid workspaceId, 
        string? searchTerm, 
        string? sortBy, 
        string? sortOrder, 
        PaginationRequest pagination, 
        CancellationToken cancellationToken = default)
    {
        var workspaceExists = await _context.Workspaces
            .AnyAsync(w => w.Id == workspaceId, cancellationToken);

        if (!workspaceExists)
        {
            _logger.LogWarning("Failed to get projects. Workspace ID {WorkspaceId} not found.", workspaceId);
            return Result.Failure<PagedResult<ProjectBriefDto>>(new Error("Workspace.NotFound", $"Workspace with ID '{workspaceId}' was not found."));
        }

        var query = _context.Projects
            .Include(p => p.Index)
            .AsNoTracking()
            .Where(p => p.WorkspaceId == workspaceId);

        // Searching
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(p => p.Name.Contains(searchTerm) || (p.Description != null && p.Description.Contains(searchTerm)));
        }

        // Sorting
        bool isDesc = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        query = sortBy?.ToLower() switch
        {
            "name" => isDesc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "projecttype" => isDesc ? query.OrderByDescending(p => p.ProjectType) : query.OrderBy(p => p.ProjectType),
            "createdat" => isDesc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt) // Default sorting
        };

        // Pagination
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var briefs = _mapper.Map<IReadOnlyList<ProjectBriefDto>>(items);
        var result = PagedResult<ProjectBriefDto>.Create(briefs, pagination.PageNumber, pagination.PageSize, totalCount);

        return Result.Success(result);
    }

    public async Task<Result<ProjectDto>> UpdateProjectAsync(Guid id, UpdateProjectDto dto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to update project: {ProjectId}", id);

        var project = await _context.Projects
            .Include(p => p.Settings)
            .Include(p => p.Statistics)
            .Include(p => p.Index)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (project == null)
        {
            _logger.LogWarning("Failed to update project. ID {ProjectId} not found.", id);
            return Result.Failure<ProjectDto>(new Error("Project.NotFound", $"Project with ID '{id}' was not found."));
        }

        var isDuplicate = await _context.Projects
            .AnyAsync(p => p.WorkspaceId == project.WorkspaceId && p.Name == dto.Name && p.Id != id, cancellationToken);

        if (isDuplicate)
        {
            _logger.LogWarning("Failed to update project {ProjectId}. Name {Name} is already taken in workspace.", id, dto.Name);
            return Result.Failure<ProjectDto>(new Error("Project.DuplicateName", "Project name is already in use in this workspace."));
        }

        // Apply changes
        _mapper.Map(dto, project);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict occurred when updating project: {ProjectId}", id);
            return Result.Failure<ProjectDto>(new Error("Concurrency.Conflict", "The project has been modified or deleted by another user."));
        }

        _logger.LogInformation("Project updated successfully: {ProjectId}", id);

        var resultDto = _mapper.Map<ProjectDto>(project);
        return Result.Success(resultDto);
    }

    public async Task<Result> DeleteProjectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to delete project: {ProjectId}", id);

        var project = await _context.Projects
            .Include(p => p.Settings)
            .Include(p => p.Statistics)
            .Include(p => p.Index)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (project == null)
        {
            _logger.LogWarning("Failed to delete project. ID {ProjectId} not found.", id);
            return Result.Failure(new Error("Project.NotFound", $"Project with ID '{id}' was not found."));
        }

        // Soft delete cascaded structures
        _context.Projects.Remove(project);
        _context.ProjectSettings.Remove(project.Settings);
        _context.ProjectIndexes.Remove(project.Index);
        
        // statistics does not inherit AuditableSoftDeleteEntity (only BaseEntity), so we delete it physically
        _context.ProjectStatistics.Remove(project.Statistics);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Project deleted successfully: {ProjectId}", id);
        return Result.Success();
    }

    public async Task<Result<ProjectSettingsDto>> GetProjectSettingsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var settings = await _context.ProjectSettings
            .FirstOrDefaultAsync(s => s.ProjectId == projectId, cancellationToken);

        if (settings == null)
        {
            _logger.LogWarning("Project settings not found for ID: {ProjectId}", projectId);
            return Result.Failure<ProjectSettingsDto>(new Error("ProjectSettings.NotFound", $"Settings for project '{projectId}' were not found."));
        }

        var dto = _mapper.Map<ProjectSettingsDto>(settings);
        return Result.Success(dto);
    }

    public async Task<Result<ProjectSettingsDto>> UpdateProjectSettingsAsync(Guid projectId, UpdateProjectSettingsDto dto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to update project settings for: {ProjectId}", projectId);

        var settings = await _context.ProjectSettings
            .FirstOrDefaultAsync(s => s.ProjectId == projectId, cancellationToken);

        if (settings == null)
        {
            _logger.LogWarning("Failed to update settings. Project ID {ProjectId} not found.", projectId);
            return Result.Failure<ProjectSettingsDto>(new Error("ProjectSettings.NotFound", $"Settings for project '{projectId}' were not found."));
        }

        _mapper.Map(dto, settings);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Project settings updated successfully for: {ProjectId}", projectId);

        var resultDto = _mapper.Map<ProjectSettingsDto>(settings);
        return Result.Success(resultDto);
    }

    public async Task<Result<ProjectStatisticsDto>> GetProjectStatisticsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var statistics = await _context.ProjectStatistics
            .FirstOrDefaultAsync(s => s.ProjectId == projectId, cancellationToken);

        if (statistics == null)
        {
            _logger.LogWarning("Project statistics not found for ID: {ProjectId}", projectId);
            return Result.Failure<ProjectStatisticsDto>(new Error("ProjectStatistics.NotFound", $"Statistics for project '{projectId}' were not found."));
        }

        var dto = _mapper.Map<ProjectStatisticsDto>(statistics);
        return Result.Success(dto);
    }
}
