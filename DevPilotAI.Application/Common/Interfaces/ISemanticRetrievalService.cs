using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.DTOs.Project;

namespace DevPilotAI.Application.Common.Interfaces;

public interface ISemanticRetrievalService
{
    Task<List<CodeChunkDto>> RetrieveRelevantContextAsync(Guid projectId, string query, CancellationToken cancellationToken = default);
}
