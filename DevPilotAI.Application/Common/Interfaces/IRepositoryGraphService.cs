using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Domain.Entities;

namespace DevPilotAI.Application.Common.Interfaces;

public class GraphSymbolNode
{
    /// <summary>Globally unique identity: Namespace::ClassName</summary>
    public string SymbolId { get; set; } = string.Empty;

    /// <summary>Short class name — used for display and type-reference matching.</summary>
    public string Name { get; set; } = string.Empty;

    public string Namespace { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public List<string> BaseTypes { get; set; } = new();
    public List<string> Fields { get; set; } = new();
    public List<string> Properties { get; set; } = new();
    public List<string> ConstructorParameters { get; set; } = new();
    public List<string> MethodParameterAndReturnTypes { get; set; } = new();
}

public interface IRepositoryGraphService
{
    Task<List<GraphSymbolNode>> GetProjectGraphNodesAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<List<CodeChunk>> GetChunksForSymbolsAsync(Guid projectId, List<string> symbolNames, CancellationToken cancellationToken = default);
    Task<List<CodeChunk>> GetArchitectureChunksAsync(Guid projectId, CancellationToken cancellationToken = default);
}
