using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Project;
using DevPilotAI.Shared.Common;
using Microsoft.AspNetCore.Mvc;

namespace DevPilotAI.Api.Controllers;

public class ProjectsController : ApiControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet("/api/workspaces/{workspaceId:guid}/projects")]
    public async Task<ActionResult<ApiResponse<PagedResult<ProjectBriefDto>>>> GetProjectsForWorkspace(
        Guid workspaceId,
        [FromQuery] string? searchTerm,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortOrder,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
        var result = await _projectService.GetProjectsByWorkspaceAsync(workspaceId, searchTerm, sortBy, sortOrder, pagination, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Workspace.NotFound")
            {
                return NotFound(ApiResponse<PagedResult<ProjectBriefDto>>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<PagedResult<ProjectBriefDto>>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<PagedResult<ProjectBriefDto>>.Success(result.Value));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> GetProject(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.GetProjectByIdAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Project.NotFound")
            {
                return NotFound(ApiResponse<ProjectDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<ProjectDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<ProjectDto>.Success(result.Value));
    }

    [HttpPost("/api/workspaces/{workspaceId:guid}/projects")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> CreateProject(Guid workspaceId, [FromBody] CreateProjectDto dto, CancellationToken cancellationToken)
    {
        var result = await _projectService.CreateProjectAsync(workspaceId, dto, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Workspace.NotFound")
            {
                return NotFound(ApiResponse<ProjectDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<ProjectDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return CreatedAtAction(nameof(GetProject), new { id = result.Value.Id }, ApiResponse<ProjectDto>.Success(result.Value, "Project created successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> UpdateProject(Guid id, [FromBody] UpdateProjectDto dto, CancellationToken cancellationToken)
    {
        var result = await _projectService.UpdateProjectAsync(id, dto, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Project.NotFound")
            {
                return NotFound(ApiResponse<ProjectDto>.Failure(result.Error.Message, result.Error.Code));
            }
            if (result.Error.Code == "Concurrency.Conflict")
            {
                return Conflict(ApiResponse<ProjectDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<ProjectDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<ProjectDto>.Success(result.Value, "Project updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteProject(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.DeleteProjectAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Project.NotFound")
            {
                return NotFound(ApiResponse.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse.Success("Project deleted successfully."));
    }

    [HttpGet("{id:guid}/settings")]
    public async Task<ActionResult<ApiResponse<ProjectSettingsDto>>> GetProjectSettings(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.GetProjectSettingsAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "ProjectSettings.NotFound")
            {
                return NotFound(ApiResponse<ProjectSettingsDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<ProjectSettingsDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<ProjectSettingsDto>.Success(result.Value));
    }

    [HttpPut("{id:guid}/settings")]
    public async Task<ActionResult<ApiResponse<ProjectSettingsDto>>> UpdateProjectSettings(Guid id, [FromBody] UpdateProjectSettingsDto dto, CancellationToken cancellationToken)
    {
        var result = await _projectService.UpdateProjectSettingsAsync(id, dto, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "ProjectSettings.NotFound")
            {
                return NotFound(ApiResponse<ProjectSettingsDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<ProjectSettingsDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<ProjectSettingsDto>.Success(result.Value, "Project settings updated successfully."));
    }

    [HttpGet("{id:guid}/statistics")]
    public async Task<ActionResult<ApiResponse<ProjectStatisticsDto>>> GetProjectStatistics(Guid id, CancellationToken cancellationToken)
    {
        var result = await _projectService.GetProjectStatisticsAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "ProjectStatistics.NotFound")
            {
                return NotFound(ApiResponse<ProjectStatisticsDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<ProjectStatisticsDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<ProjectStatisticsDto>.Success(result.Value));
    }
}
