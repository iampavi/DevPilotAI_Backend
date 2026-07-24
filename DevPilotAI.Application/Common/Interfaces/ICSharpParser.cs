using System.Collections.Generic;
using DevPilotAI.Shared.Common;

namespace DevPilotAI.Application.Common.Interfaces;

public interface ICSharpParser
{
    Result<ParsedFileData> ParseContent(string sourceCode);
}

public record ParsedFileData(
    List<string> Usings,
    List<ParsedClassData> Classes
);

public record ParsedClassData(
    string Name,
    string FullName,
    string? Namespace,
    string SymbolType, // Class, Interface, Record, Struct, Enum
    List<string> BaseTypes,
    List<string> Attributes,
    int StartLine,
    int EndLine,
    List<ParsedMethodData> Methods,
    List<ParsedPropertyData> Properties,
    List<ParsedFieldData> Fields
);

public record ParsedMethodData(
    string Name,
    string? ReturnType,
    string AccessModifier,
    List<string> Parameters,
    List<string> Attributes,
    int StartLine,
    int EndLine
);

public record ParsedPropertyData(
    string Name,
    string Type,
    string AccessModifier,
    List<string> Attributes,
    int StartLine,
    int EndLine
);

public record ParsedFieldData(
    string Name,
    string Type,
    string AccessModifier,
    List<string> Attributes,
    int StartLine,
    int EndLine
);
