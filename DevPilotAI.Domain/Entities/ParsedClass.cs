using System;
using System.Collections.Generic;
using DevPilotAI.Domain.Common;
using DevPilotAI.Domain.Enums;

namespace DevPilotAI.Domain.Entities;

public class ParsedClass : BaseEntity
{
    public Guid ParsedFileId { get; set; }
    public ParsedFile ParsedFile { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty; // Namespace + Name
    public string? Namespace { get; set; }
    
    public SymbolType SymbolType { get; set; }
    
    // Serialized lists
    public List<string> BaseTypes { get; set; } = new();
    public List<string> Attributes { get; set; } = new();

    public int StartLine { get; set; }
    public int EndLine { get; set; }

    public ICollection<ParsedMethod> Methods { get; set; } = new List<ParsedMethod>();
    public ICollection<ParsedProperty> Properties { get; set; } = new List<ParsedProperty>();
    public ICollection<ParsedField> Fields { get; set; } = new List<ParsedField>();
}
