using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.DTOs.Chat;
using DevPilotAI.Application.DTOs.Project;
using Microsoft.Extensions.Configuration;

namespace DevPilotAI.Infrastructure.Services;

public class RepositoryContextExpander : IRepositoryContextExpander
{
    private readonly IRepositoryGraphService _graphService;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;

    public RepositoryContextExpander(
        IRepositoryGraphService graphService,
        IMapper mapper,
        IConfiguration configuration)
    {
        _graphService = graphService;
        _mapper = mapper;
        _configuration = configuration;
    }

    public async Task<RepositoryContextDto> ExpandContextAsync(
        Guid projectId,
        List<CodeChunkDto> seedChunks,
        List<string> additionalTargetSymbols,
        CancellationToken cancellationToken = default)
    {
        var defaultDepth = int.TryParse(_configuration["RagSettings:ContextExpansion:DefaultDepth"], out var dVal) ? dVal : 2;
        var maxSymbols = int.TryParse(_configuration["RagSettings:ContextExpansion:MaxSymbols"], out var mVal) ? mVal : 50;

        var seedSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seedChunkIds = new HashSet<Guid>(seedChunks.Select(c => c.Id));

        // 1. Extract symbols from seed chunks
        foreach (var chunk in seedChunks)
        {
            try
            {
                var metaDict = JsonSerializer.Deserialize<Dictionary<string, string>>(chunk.Metadata);
                if (metaDict != null)
                {
                    if (metaDict.TryGetValue("class_name", out var cls) && !string.IsNullOrEmpty(cls))
                        seedSymbols.Add(cls);
                    if (metaDict.TryGetValue("symbol_name", out var sym) && !string.IsNullOrEmpty(sym))
                        seedSymbols.Add(sym);
                }
            }
            catch { }
        }

        // Add additional seed targets
        if (additionalTargetSymbols != null)
        {
            foreach (var sym in additionalTargetSymbols)
            {
                if (!string.IsNullOrEmpty(sym))
                    seedSymbols.Add(sym);
            }
        }

        // 2. Load all project graph nodes.
        // KEY FIX: use GroupBy instead of ToDictionary.
        // Multiple classes in different namespaces can share the same short name
        // (e.g., AddProfileImage in UserController AND AdminController).
        // ToDictionary would throw on duplicate keys; GroupBy handles them cleanly.
        var graphNodes = await _graphService.GetProjectGraphNodesAsync(projectId, cancellationToken);
        var graphNodesByName = graphNodes
            .GroupBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 3. BFS traversal.
        // Queue carries the SHORT name (as it appears in type references) and depth.
        // visited is keyed by SymbolId (Namespace::Name) — globally unique identity.
        var queue = new Queue<(string SymbolName, int Depth)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // keyed by SymbolId
        var visitedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // keyed by short Name
        var relationships = new List<RepositoryRelationshipDto>();

        foreach (var sym in seedSymbols)
        {
            queue.Enqueue((sym, 0));
        }

        while (queue.Any() && visited.Count < maxSymbols)
        {
            var (currentSymbol, depth) = queue.Dequeue();

            // Skip if we have already processed every class that carries this short name
            if (visitedNames.Contains(currentSymbol))
                continue;

            visitedNames.Add(currentSymbol);

            if (depth >= defaultDepth)
                continue;

            // Find all graph nodes whose short Name matches (fan-out across namespaces)
            var matchingNodes = graphNodesByName.TryGetValue(currentSymbol, out var nodes)
                ? nodes
                : new List<GraphSymbolNode>();

            // Check implementing classes when the symbol looks like an interface (IXxx)
            if (currentSymbol.StartsWith("I") && currentSymbol.Length > 1 && char.IsUpper(currentSymbol[1]))
            {
                foreach (var node in graphNodes)
                {
                    if (visited.Contains(node.SymbolId)) continue;
                    if (node.BaseTypes.Contains(currentSymbol, StringComparer.OrdinalIgnoreCase))
                    {
                        relationships.Add(new RepositoryRelationshipDto
                        {
                            FromSymbol = node.Name,
                            RelationshipType = "implements",
                            ToSymbol = currentSymbol
                        });
                        queue.Enqueue((node.Name, depth + 1));
                    }
                }
            }

            // Process each class that carries this short name
            foreach (var currentNode in matchingNodes)
            {
                if (visited.Contains(currentNode.SymbolId))
                    continue;

                visited.Add(currentNode.SymbolId);

                // Inherits / Implements
                foreach (var baseType in currentNode.BaseTypes)
                {
                    relationships.Add(new RepositoryRelationshipDto
                    {
                        FromSymbol = currentNode.Name,
                        RelationshipType = baseType.StartsWith("I") ? "implements" : "inherits",
                        ToSymbol = baseType
                    });
                    queue.Enqueue((baseType, depth + 1));
                }

                // Constructor Parameter Injections (injects)
                foreach (var param in currentNode.ConstructorParameters)
                {
                    relationships.Add(new RepositoryRelationshipDto
                    {
                        FromSymbol = currentNode.Name,
                        RelationshipType = "injects",
                        ToSymbol = param
                    });
                    queue.Enqueue((param, depth + 1));
                }

                // Field types (uses)
                foreach (var fieldType in currentNode.Fields)
                {
                    relationships.Add(new RepositoryRelationshipDto
                    {
                        FromSymbol = currentNode.Name,
                        RelationshipType = "uses",
                        ToSymbol = fieldType
                    });
                    queue.Enqueue((fieldType, depth + 1));
                }

                // Property types (uses)
                foreach (var propType in currentNode.Properties)
                {
                    relationships.Add(new RepositoryRelationshipDto
                    {
                        FromSymbol = currentNode.Name,
                        RelationshipType = "uses",
                        ToSymbol = propType
                    });
                    queue.Enqueue((propType, depth + 1));
                }

                // Method return/parameter types (uses)
                foreach (var usageType in currentNode.MethodParameterAndReturnTypes)
                {
                    relationships.Add(new RepositoryRelationshipDto
                    {
                        FromSymbol = currentNode.Name,
                        RelationshipType = "uses",
                        ToSymbol = usageType
                    });
                    queue.Enqueue((usageType, depth + 1));
                }

                // Upstream dependents (classes that reference this class)
                foreach (var node in graphNodes)
                {
                    if (visited.Contains(node.SymbolId)) continue;
                    if (node.Name.Equals(currentNode.Name, StringComparison.OrdinalIgnoreCase)) continue;

                    bool isDependent = false;
                    if (node.BaseTypes.Contains(currentNode.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        relationships.Add(new RepositoryRelationshipDto
                        {
                            FromSymbol = node.Name,
                            RelationshipType = "implements",
                            ToSymbol = currentNode.Name
                        });
                        isDependent = true;
                    }
                    if (node.ConstructorParameters.Contains(currentNode.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        relationships.Add(new RepositoryRelationshipDto
                        {
                            FromSymbol = node.Name,
                            RelationshipType = "injects",
                            ToSymbol = currentNode.Name
                        });
                        isDependent = true;
                    }
                    if (node.Fields.Contains(currentNode.Name, StringComparer.OrdinalIgnoreCase) ||
                        node.Properties.Contains(currentNode.Name, StringComparer.OrdinalIgnoreCase) ||
                        node.MethodParameterAndReturnTypes.Contains(currentNode.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        relationships.Add(new RepositoryRelationshipDto
                        {
                            FromSymbol = node.Name,
                            RelationshipType = "uses",
                            ToSymbol = currentNode.Name
                        });
                        isDependent = true;
                    }

                    if (isDependent)
                    {
                        queue.Enqueue((node.Name, depth + 1));
                    }
                }
            }
        }

        // Distinct relationships
        var distinctRelationships = relationships
            .GroupBy(r => $"{r.FromSymbol}:{r.RelationshipType}:{r.ToSymbol}")
            .Select(g => g.First())
            .ToList();

        // 4. Retrieve expanded chunks for all visited symbols
        var expandedChunksList = new List<CodeChunkDto>();
        var visitedSymbolsList = visited.ToList();
        var chunks = await _graphService.GetChunksForSymbolsAsync(projectId, visitedSymbolsList, cancellationToken);

        foreach (var ch in chunks)
        {
            if (seedChunkIds.Contains(ch.Id))
                continue; // Skip duplicate chunk IDs

            var dto = _mapper.Map<CodeChunkDto>(ch);
            expandedChunksList.Add(dto);
        }

        // 5. Gather referenced symbols and related file paths
        var referencedSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var relatedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in seedChunks.Concat(expandedChunksList))
        {
            try
            {
                var metaDict = JsonSerializer.Deserialize<Dictionary<string, string>>(chunk.Metadata);
                if (metaDict != null)
                {
                    if (metaDict.TryGetValue("symbol_name", out var sym) && !string.IsNullOrEmpty(sym))
                        referencedSymbols.Add(sym);
                    else if (metaDict.TryGetValue("class_name", out var cls) && !string.IsNullOrEmpty(cls))
                        referencedSymbols.Add(cls);

                    if (metaDict.TryGetValue("file_path", out var path) && !string.IsNullOrEmpty(path))
                        relatedFiles.Add(path);
                }
            }
            catch { }
        }

        return new RepositoryContextDto
        {
            SeedChunks = seedChunks,
            ExpandedChunks = expandedChunksList,
            Relationships = distinctRelationships,
            ReferencedSymbols = referencedSymbols.ToList(),
            RelatedFiles = relatedFiles.ToList(),
            ExpansionDepth = defaultDepth,
            ExpandedSymbolCount = visited.Count
        };
    }
}
