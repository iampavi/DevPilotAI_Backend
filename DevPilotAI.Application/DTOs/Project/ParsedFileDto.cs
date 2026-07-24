using System;
using System.Collections.Generic;

namespace DevPilotAI.Application.DTOs.Project;

public class ParsedFileDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public int ParserVersion { get; set; }
    public List<string> Usings { get; set; } = new();
}
