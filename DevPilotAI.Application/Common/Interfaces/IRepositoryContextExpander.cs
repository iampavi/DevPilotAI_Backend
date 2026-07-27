using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.DTOs.Chat;
using DevPilotAI.Application.DTOs.Project;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IRepositoryContextExpander
{
    Task<RepositoryContextDto> ExpandContextAsync(
        Guid projectId,
        List<CodeChunkDto> seedChunks,
        List<string> additionalTargetSymbols,
        CancellationToken cancellationToken = default);
}
