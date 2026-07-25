using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class Program
{
    public static void Main()
    {
        Console.WriteLine("Debugging retrieval filtering...");

        var ignoredDirs = new List<string> { "bin", "obj", "node_modules", ".git", "vendor", "dist", "build", "coverage", ".vs", ".idea", ".vscode" };
        var ignoredFiles = new List<string> { "package-lock.json", "yarn.lock", "pnpm-lock.yaml", "composer.lock", "packages.lock.json" };

        var filePath = "class"; // fallback from ChunkType "Class"
        var chunkType = "class";
        var fileName = Path.GetFileName(filePath);

        Console.WriteLine($"filePath: '{filePath}'");
        Console.WriteLine($"fileName: '{fileName}'");

        // Ignored Directories check
        bool inIgnoredDir = ignoredDirs.Any(d => filePath.Contains("/" + d.ToLowerInvariant() + "/") || 
                                                 filePath.Contains("\\" + d.ToLowerInvariant() + "\\") || 
                                                 filePath.StartsWith(d.ToLowerInvariant() + "/") || 
                                                 filePath.StartsWith(d.ToLowerInvariant() + "\\"));
        Console.WriteLine($"inIgnoredDir: {inIgnoredDir}");

        // Ignored Files check
        bool isIgnoredFile = ignoredFiles.Any(f => f.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        Console.WriteLine($"isIgnoredFile: {isIgnoredFile}");

        // Ignored Extensions check
        bool isIgnoredExt = fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith(".cache", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith(".min.css", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith(".map", StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"isIgnoredExt: {isIgnoredExt}");
    }
}
