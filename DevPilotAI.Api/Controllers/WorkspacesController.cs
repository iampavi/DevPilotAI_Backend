using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Workspace;
using DevPilotAI.Shared.Common;
using Microsoft.AspNetCore.Mvc;

namespace DevPilotAI.Api.Controllers;

public class WorkspacesController : ApiControllerBase
{
    private readonly IWorkspaceService _workspaceService;

    public WorkspacesController(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkspaceBriefDto>>>> GetWorkspaces(
        [FromQuery] string? searchTerm,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortOrder,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
        var result = await _workspaceService.GetWorkspacesAsync(searchTerm, sortBy, sortOrder, pagination, cancellationToken);
        
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<PagedResult<WorkspaceBriefDto>>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<PagedResult<WorkspaceBriefDto>>.Success(result.Value));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> GetWorkspace(Guid id, CancellationToken cancellationToken)
    {
        var result = await _workspaceService.GetWorkspaceByIdAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Workspace.NotFound")
            {
                return NotFound(ApiResponse<WorkspaceDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<WorkspaceDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<WorkspaceDto>.Success(result.Value));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> CreateWorkspace([FromBody] CreateWorkspaceDto dto, CancellationToken cancellationToken)
    {
        var result = await _workspaceService.CreateWorkspaceAsync(dto, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<WorkspaceDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return CreatedAtAction(nameof(GetWorkspace), new { id = result.Value.Id }, ApiResponse<WorkspaceDto>.Success(result.Value, "Workspace created successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<WorkspaceDto>>> UpdateWorkspace(Guid id, [FromBody] UpdateWorkspaceDto dto, CancellationToken cancellationToken)
    {
        var result = await _workspaceService.UpdateWorkspaceAsync(id, dto, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Workspace.NotFound")
            {
                return NotFound(ApiResponse<WorkspaceDto>.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse<WorkspaceDto>.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse<WorkspaceDto>.Success(result.Value, "Workspace updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteWorkspace(Guid id, CancellationToken cancellationToken)
    {
        var result = await _workspaceService.DeleteWorkspaceAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == "Workspace.NotFound")
            {
                return NotFound(ApiResponse.Failure(result.Error.Message, result.Error.Code));
            }
            return BadRequest(ApiResponse.Failure(result.Error.Message, result.Error.Code));
        }

        return Ok(ApiResponse.Success("Workspace deleted successfully."));
    }
}
