using System;
using System.Collections.Generic;

namespace DevPilotAI.Application.DTOs.Project;

public class ParsedMethodDto
{
    public Guid Id { get; set; }
    public Guid ParsedClassId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ReturnType { get; set; }
    public string AccessModifier { get; set; } = string.Empty;
    public List<string> Parameters { get; set; } = new();
    public List<string> Attributes { get; set; } = new();
    public int StartLine { get; set; }
    public int EndLine { get; set; }
}
