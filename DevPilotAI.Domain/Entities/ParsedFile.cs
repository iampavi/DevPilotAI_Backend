using System;
using System.Collections.Generic;
using DevPilotAI.Domain.Common;

namespace DevPilotAI.Domain.Entities;

public class ParsedFile : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string RelativePath { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public int ParserVersion { get; set; } = 1;

    // Serialized list of using imports
    public List<string> Usings { get; set; } = new();

    public ICollection<ParsedClass> Classes { get; set; } = new List<ParsedClass>();
}
