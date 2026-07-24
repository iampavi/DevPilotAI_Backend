using System;
using System.Collections.Generic;
using DevPilotAI.Domain.Common;

namespace DevPilotAI.Domain.Entities;

public class ParsedMethod : BaseEntity
{
    public Guid ParsedClassId { get; set; }
    public ParsedClass ParsedClass { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? ReturnType { get; set; }
    public string AccessModifier { get; set; } = string.Empty;

    // Serialized parameters (e.g. "int id", "string name")
    public List<string> Parameters { get; set; } = new();
    
    // Serialized attributes
    public List<string> Attributes { get; set; } = new();

    public int StartLine { get; set; }
    public int EndLine { get; set; }
}
