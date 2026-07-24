using System;
using System.Collections.Generic;
using DevPilotAI.Domain.Common;

namespace DevPilotAI.Domain.Entities;

public class ParsedProperty : BaseEntity
{
    public Guid ParsedClassId { get; set; }
    public ParsedClass ParsedClass { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string AccessModifier { get; set; } = string.Empty;

    // Serialized attributes
    public List<string> Attributes { get; set; } = new();

    public int StartLine { get; set; }
    public int EndLine { get; set; }
}
