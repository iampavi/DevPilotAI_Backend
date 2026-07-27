using System;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Copilot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevPilotAI.Api.Controllers;

[Authorize]
public class CopilotController : ApiControllerBase
{
    private readonly ICopilotService _copilotService;

    public CopilotController(ICopilotService copilotService)
    {
        _copilotService = copilotService;
    }

    [HttpPost("/api/projects/{projectId:guid}/copilot")]
    public async Task<ActionResult<CopilotResponseDto>> ExecuteCopilotAction(
        Guid projectId,
        [FromBody] CopilotRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest("Request body cannot be null.");
        }

        try
        {
            var response = await _copilotService.ExecuteAsync(projectId, request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred processing the copilot action: {ex.Message}");
        }
    }
}
