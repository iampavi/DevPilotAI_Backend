using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.DTOs.Project;

namespace DevPilotAI.Application.Common.Interfaces;

public interface ISemanticSearchService
{
    Task<List<CodeChunkDto>> SearchChunksAsync(Guid projectId, string query, int limit = 5, CancellationToken cancellationToken = default);
}
