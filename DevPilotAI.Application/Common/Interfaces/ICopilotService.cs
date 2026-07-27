using System;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.DTOs.Copilot;

namespace DevPilotAI.Application.Common.Interfaces;

public interface ICopilotService
{
    Task<CopilotResponseDto> ExecuteAsync(
        Guid projectId,
        CopilotRequest request,
        CancellationToken cancellationToken);
}
