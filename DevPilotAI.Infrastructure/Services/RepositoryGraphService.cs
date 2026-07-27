using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Infrastructure.Services;

public class RepositoryGraphService : IRepositoryGraphService
{
    private readonly IApplicationDbContext _context;

    public RepositoryGraphService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<GraphSymbolNode>> GetProjectGraphNodesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var allClasses = await _context.ParsedClasses
            .Include(c => c.Methods)
            .Include(c => c.Fields)
            .Include(c => c.Properties)
            .Include(c => c.ParsedFile)
            .Where(c => c.ParsedFile.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        var nodes = new List<GraphSymbolNode>();

        foreach (var c in allClasses)
        {
            var ns = c.Namespace ?? "Global";
            var node = new GraphSymbolNode
            {
                SymbolId = $"{ns}::{c.Name}",
                Name = c.Name,
                Namespace = ns,
                FilePath = c.ParsedFile?.RelativePath ?? string.Empty,
                BaseTypes = c.BaseTypes ?? new List<string>(),
                Fields = c.Fields.Select(f => f.Type).ToList(),
                Properties = c.Properties.Select(p => p.Type).ToList()
            };

            // Constructor dependencies (injects)
            var ctors = c.Methods.Where(m => m.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase) || m.Name.Equals(".ctor", StringComparison.OrdinalIgnoreCase));
            foreach (var ctor in ctors)
            {
                foreach (var param in ctor.Parameters)
                {
                    var parts = param.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        node.ConstructorParameters.Add(parts[0]);
                    }
                }
            }

            // Method return & parameter types (calls/uses)
            foreach (var method in c.Methods)
            {
                if (!string.IsNullOrEmpty(method.ReturnType))
                {
                    node.MethodParameterAndReturnTypes.Add(method.ReturnType);
                }
                foreach (var param in method.Parameters)
                {
                    var parts = param.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        node.MethodParameterAndReturnTypes.Add(parts[0]);
                    }
                }
            }

            // Distinct lists
            node.BaseTypes = node.BaseTypes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            node.Fields = node.Fields.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            node.Properties = node.Properties.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            node.ConstructorParameters = node.ConstructorParameters.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            node.MethodParameterAndReturnTypes = node.MethodParameterAndReturnTypes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            nodes.Add(node);
        }

        return nodes;
    }

    public async Task<List<CodeChunk>> GetChunksForSymbolsAsync(Guid projectId, List<string> symbolNames, CancellationToken cancellationToken = default)
    {
        var classIds = await _context.ParsedClasses
            .Where(c => c.ParsedFile.ProjectId == projectId && symbolNames.Contains(c.Name))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        return await _context.CodeChunks
            .Include(ch => ch.ParsedFile)
            .Where(ch => ch.ProjectId == projectId &&
                (ch.ParsedClassId != null && classIds.Contains(ch.ParsedClassId.Value)))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CodeChunk>> GetArchitectureChunksAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await _context.CodeChunks
            .Include(ch => ch.ParsedFile)
            .Where(ch => ch.ProjectId == projectId &&
                (ch.ParsedFile.RelativePath.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase) ||
                 ch.ParsedFile.RelativePath.EndsWith("Startup.cs", StringComparison.OrdinalIgnoreCase) ||
                 ch.ParsedFile.RelativePath.Contains("DbContext") ||
                 ch.ParsedFile.RelativePath.Contains("Middleware") ||
                 ch.ParsedFile.RelativePath.Contains("Configuration") ||
                 ch.ParsedFile.RelativePath.Contains("DependencyInjection")))
            .Take(15)
            .ToListAsync(cancellationToken);
    }
}
