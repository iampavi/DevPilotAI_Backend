using System;
using System.Collections.Generic;

namespace DevPilotAI.Application.DTOs.Project;

public class ParsedClassDto
{
    public Guid Id { get; set; }
    public Guid ParsedFileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public string SymbolType { get; set; } = string.Empty;
    public List<string> BaseTypes { get; set; } = new();
    public List<string> Attributes { get; set; } = new();
    public int StartLine { get; set; }
    public int EndLine { get; set; }
}
