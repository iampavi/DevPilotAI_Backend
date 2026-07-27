using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Infrastructure.Services;

public class SymbolGraphResolver
{
    private readonly ApplicationDbContext _context;

    public SymbolGraphResolver(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<string>> GetClassDependenciesAsync(Guid projectId, string className, CancellationToken cancellationToken = default)
    {
        var result = new List<string>();

        var allProjectClasses = await _context.ParsedClasses
            .Include(c => c.Methods)
            .Include(c => c.ParsedFile)
            .Where(c => c.ParsedFile.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        var parsedClass = allProjectClasses
            .FirstOrDefault(c => c.Name.Equals(className, StringComparison.OrdinalIgnoreCase));

        if (parsedClass == null) return result;

        // Base Types / Interfaces
        foreach (var baseType in parsedClass.BaseTypes)
        {
            result.Add($"Inherits/Implements: {baseType}");
        }

        // Constructor Parameters
        var constructors = parsedClass.Methods
            .Where(m => m.Name.Equals(className, StringComparison.OrdinalIgnoreCase) || m.Name.Equals(".ctor", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var ctor in constructors)
        {
            foreach (var param in ctor.Parameters)
            {
                var parts = param.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    result.Add($"Constructor Injected: {parts[0]}");
                }
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<List<string>> GetClassDependentsAsync(Guid projectId, string className, CancellationToken cancellationToken = default)
    {
        var result = new List<string>();
        var interfaceName = className.StartsWith("I") ? className : "I" + className;

        var allProjectClasses = await _context.ParsedClasses
            .Include(c => c.Methods)
            .Include(c => c.ParsedFile)
            .Where(c => c.ParsedFile.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        foreach (var cClass in allProjectClasses)
        {
            if (cClass.Name.Equals(className, StringComparison.OrdinalIgnoreCase))
                continue;

            // Check if inherits className or its interface
            bool inherits = cClass.BaseTypes.Any(b => 
                b.Equals(className, StringComparison.OrdinalIgnoreCase) || 
                b.Equals(interfaceName, StringComparison.OrdinalIgnoreCase));

            if (inherits)
            {
                result.Add($"{cClass.Name} implements/inherits {className}");
            }

            // Check constructor parameters
            var constructors = cClass.Methods
                .Where(m => m.Name.Equals(cClass.Name, StringComparison.OrdinalIgnoreCase) || m.Name.Equals(".ctor", StringComparison.OrdinalIgnoreCase));

            foreach (var ctor in constructors)
            {
                foreach (var param in ctor.Parameters)
                {
                    var parts = param.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var typeName = parts[0];
                        if (typeName.Equals(className, StringComparison.OrdinalIgnoreCase) || 
                            typeName.Equals(interfaceName, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add($"{cClass.Name} injects {typeName} via constructor");
                        }
                    }
                }
            }
        }

        return result.Distinct().ToList();
    }

    public async Task<List<string>> GetSymbolNavigationPathAsync(Guid projectId, string symbolName, CancellationToken cancellationToken = default)
    {
        var allProjectClasses = await _context.ParsedClasses
            .Include(c => c.Methods)
            .Include(c => c.ParsedFile)
            .Where(c => c.ParsedFile.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = new List<string>();

        TraverseNavigationInMemory(allProjectClasses, symbolName, path, visited, 0);
        return path;
    }

    private void TraverseNavigationInMemory(
        List<ParsedClass> allClassList,
        string currentSymbol, 
        List<string> path, 
        HashSet<string> visited, 
        int depth)
    {
        if (depth > 5 || visited.Contains(currentSymbol))
            return;

        visited.Add(currentSymbol);

        // Find matching class
        var parsedClass = allClassList
            .FirstOrDefault(c => c.Name.Equals(currentSymbol, StringComparison.OrdinalIgnoreCase));

        if (parsedClass == null)
        {
            // Try matching interface implementation
            var implementingClass = allClassList
                .FirstOrDefault(c => c.BaseTypes.Any(b => b.Equals(currentSymbol, StringComparison.OrdinalIgnoreCase)));

            if (implementingClass != null)
            {
                path.Add($"{currentSymbol} ➔ Implemented by: {implementingClass.Name}");
                TraverseNavigationInMemory(allClassList, implementingClass.Name, path, visited, depth + 1);
            }
            else
            {
                path.Add(currentSymbol);
            }
            return;
        }

        // If it's a class, find its constructor dependencies
        var constructors = parsedClass.Methods
            .Where(m => m.Name.Equals(currentSymbol, StringComparison.OrdinalIgnoreCase) || m.Name.Equals(".ctor", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var dependencies = new List<string>();
        foreach (var ctor in constructors)
        {
            foreach (var param in ctor.Parameters)
            {
                var parts = param.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    dependencies.Add(parts[0]);
                }
            }
        }

        if (dependencies.Any())
        {
            path.Add($"{currentSymbol} (Injects: {string.Join(", ", dependencies)})");
            foreach (var dep in dependencies)
            {
                TraverseNavigationInMemory(allClassList, dep, path, visited, depth + 1);
            }
        }
        else
        {
            path.Add(currentSymbol);
        }
    }
}
