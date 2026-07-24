using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Project;
using DevPilotAI.Shared.Common;
using Microsoft.AspNetCore.Http;
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

    [HttpPost("/api/workspaces/{workspaceId:guid}/projects/import/local")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> RegisterLocal(
        Guid workspaceId,
        [FromBody] RegisterLocalDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.RegisterLocalProjectAsync(workspaceId, dto, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Workspace.NotFound")
            {
                return NotFound(ApiResponse<ProjectDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<ProjectDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return CreatedAtAction(nameof(GetProject), new { id = result.Value.Id }, ApiResponse<ProjectDto>.Success(result.Value, "Local project registered successfully."));
    }

    [HttpPost("/api/workspaces/{workspaceId:guid}/projects/import/zip")]
    public async Task<ActionResult<ApiResponse<ProjectImportJobDto>>> ImportZip(
        Guid workspaceId,
        [FromForm] string name,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<ProjectImportJobDto>.Failure("No file was uploaded.", "Project.NoFileUploaded"));
        }

        using var stream = file.OpenReadStream();
        var result = await _projectService.ImportZipProjectAsync(workspaceId, name, stream, file.FileName, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Workspace.NotFound")
            {
                return NotFound(ApiResponse<ProjectImportJobDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<ProjectImportJobDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Accepted(ApiResponse<ProjectImportJobDto>.Success(result.Value, "ZIP import queued. Check job status for progress."));
    }

    [HttpPost("/api/workspaces/{workspaceId:guid}/projects/import/git")]
    public async Task<ActionResult<ApiResponse<ProjectImportJobDto>>> ImportGit(
        Guid workspaceId,
        [FromBody] ImportGitDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.ImportGitProjectAsync(workspaceId, dto, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Workspace.NotFound")
            {
                return NotFound(ApiResponse<ProjectImportJobDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<ProjectImportJobDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Accepted(ApiResponse<ProjectImportJobDto>.Success(result.Value, "Git clone and import queued. Check job status for progress."));
    }

    [HttpGet("{projectId:guid}/import-jobs")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProjectImportJobDto>>>> GetProjectImportJobs(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.GetProjectImportJobsAsync(projectId, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Project.NotFound")
            {
                return NotFound(ApiResponse<IReadOnlyList<ProjectImportJobDto>>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<IReadOnlyList<ProjectImportJobDto>>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<IReadOnlyList<ProjectImportJobDto>>.Success(result.Value));
    }

    [HttpGet("/api/projects/import-jobs/{jobId:guid}")]
    public async Task<ActionResult<ApiResponse<ProjectImportJobDto>>> GetProjectImportJob(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.GetProjectImportJobByIdAsync(jobId, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "ImportJob.NotFound")
            {
                return NotFound(ApiResponse<ProjectImportJobDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<ProjectImportJobDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<ProjectImportJobDto>.Success(result.Value));
    }

    [HttpPost("/api/projects/{projectId:guid}/parse")]
    public async Task<ActionResult<ApiResponse<ProjectParseJobDto>>> ParseProject(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.ParseProjectAsync(projectId, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Project.NotFound")
            {
                return NotFound(ApiResponse<ProjectParseJobDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<ProjectParseJobDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Accepted(ApiResponse<ProjectParseJobDto>.Success(result.Value, "Project parsing queued. Check status for progress."));
    }

    [HttpGet("/api/projects/{projectId:guid}/parse-jobs")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProjectParseJobDto>>>> GetProjectParseJobs(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.GetProjectParseJobsAsync(projectId, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Project.NotFound")
            {
                return NotFound(ApiResponse<IReadOnlyList<ProjectParseJobDto>>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<IReadOnlyList<ProjectParseJobDto>>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<IReadOnlyList<ProjectParseJobDto>>.Success(result.Value));
    }

    [HttpGet("/api/projects/parse-jobs/{jobId:guid}")]
    public async Task<ActionResult<ApiResponse<ProjectParseJobDto>>> GetProjectParseJob(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.GetProjectParseJobByIdAsync(jobId, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "ParseJob.NotFound")
            {
                return NotFound(ApiResponse<ProjectParseJobDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<ProjectParseJobDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<ProjectParseJobDto>.Success(result.Value));
    }

    [HttpPost("/api/projects/{projectId:guid}/chunk")]
    public async Task<ActionResult<ApiResponse<ProjectChunkingJobDto>>> ChunkProject(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.ChunkProjectAsync(projectId, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Project.NotFound")
            {
                return NotFound(ApiResponse<ProjectChunkingJobDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<ProjectChunkingJobDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Accepted(ApiResponse<ProjectChunkingJobDto>.Success(result.Value, "Project chunking and embedding queued."));
    }

    [HttpGet("/api/projects/{projectId:guid}/chunk-jobs")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProjectChunkingJobDto>>>> GetProjectChunkingJobs(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.GetProjectChunkingJobsAsync(projectId, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Project.NotFound")
            {
                return NotFound(ApiResponse<IReadOnlyList<ProjectChunkingJobDto>>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<IReadOnlyList<ProjectChunkingJobDto>>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<IReadOnlyList<ProjectChunkingJobDto>>.Success(result.Value));
    }

    [HttpGet("/api/projects/chunk-jobs/{jobId:guid}")]
    public async Task<ActionResult<ApiResponse<ProjectChunkingJobDto>>> GetProjectChunkingJob(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.GetProjectChunkingJobByIdAsync(jobId, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "ChunkingJob.NotFound")
            {
                return NotFound(ApiResponse<ProjectChunkingJobDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<ProjectChunkingJobDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<ProjectChunkingJobDto>.Success(result.Value));
    }

    [HttpGet("/api/projects/{projectId:guid}/chunks")]
    public async Task<ActionResult<ApiResponse<PagedResult<CodeChunkDto>>>> GetProjectChunks(
        Guid projectId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? chunkType = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _projectService.GetProjectChunksAsync(projectId, pageNumber, pageSize, chunkType, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Project.NotFound")
            {
                return NotFound(ApiResponse<PagedResult<CodeChunkDto>>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<PagedResult<CodeChunkDto>>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<PagedResult<CodeChunkDto>>.Success(result.Value));
    }
}
