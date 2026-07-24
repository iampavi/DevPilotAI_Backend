using System;
using System.Collections.Generic;
using System.Linq;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Shared.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DevPilotAI.Infrastructure.Services;

public class RoslynCSharpParser : ICSharpParser
{
    public Result<ParsedFileData> ParseContent(string sourceCode)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();
            
            var walker = new CodeStructureWalker(tree);
            walker.Visit(root);

            var usings = root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Select(u => u.Name.ToString())
                .ToList();

            return Result.Success(new ParsedFileData(usings, walker.Classes));
        }
        catch (Exception ex)
        {
            return Result.Failure<ParsedFileData>(new Error("Roslyn.ParseError", ex.Message));
        }
    }

    private class CodeStructureWalker : CSharpSyntaxWalker
    {
        private readonly SyntaxTree _tree;
        public List<ParsedClassData> Classes { get; } = new();

        public CodeStructureWalker(SyntaxTree tree) : base(SyntaxWalkerDepth.Node)
        {
            _tree = tree;
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            Classes.Add(ParseTypeDeclaration(node, "Class"));
            base.VisitClassDeclaration(node);
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            Classes.Add(ParseTypeDeclaration(node, "Interface"));
            base.VisitInterfaceDeclaration(node);
        }

        public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            Classes.Add(ParseTypeDeclaration(node, "Record"));
            base.VisitRecordDeclaration(node);
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            Classes.Add(ParseTypeDeclaration(node, "Struct"));
            base.VisitStructDeclaration(node);
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            Classes.Add(ParseTypeDeclaration(node, "Enum"));
            base.VisitEnumDeclaration(node);
        }

        private ParsedClassData ParseTypeDeclaration(BaseTypeDeclarationSyntax node, string symbolType)
        {
            var name = node.Identifier.Text;
            var ns = GetNamespace(node);
            var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

            var baseTypes = node.BaseList?.Types
                .Select(t => t.Type.ToString())
                .ToList() ?? new List<string>();

            var attributes = ExtractAttributes(node.AttributeLists);

            var span = _tree.GetLineSpan(node.Span);
            var startLine = span.StartLinePosition.Line + 1;
            var endLine = span.EndLinePosition.Line + 1;

            var methods = new List<ParsedMethodData>();
            var properties = new List<ParsedPropertyData>();
            var fields = new List<ParsedFieldData>();

            if (node is TypeDeclarationSyntax typeNode)
            {
                methods = typeNode.Members.OfType<MethodDeclarationSyntax>()
                    .Select(ParseMethod)
                    .ToList();

                // Add constructors as methods
                var ctors = typeNode.Members.OfType<ConstructorDeclarationSyntax>()
                    .Select(ParseConstructor)
                    .ToList();
                methods.AddRange(ctors);

                properties = typeNode.Members.OfType<PropertyDeclarationSyntax>()
                    .Select(ParseProperty)
                    .ToList();

                fields = typeNode.Members.OfType<FieldDeclarationSyntax>()
                    .SelectMany(ParseFields)
                    .ToList();
            }

            return new ParsedClassData(
                name, fullName, ns, symbolType, baseTypes, attributes,
                startLine, endLine, methods, properties, fields);
        }

        private ParsedMethodData ParseMethod(MethodDeclarationSyntax node)
        {
            var name = node.Identifier.Text;
            var returnType = node.ReturnType.ToString();
            var access = GetAccessModifier(node.Modifiers);
            var attributes = ExtractAttributes(node.AttributeLists);

            var parameters = node.ParameterList.Parameters
                .Select(p => $"{p.Type} {p.Identifier.Text}".Trim())
                .ToList();

            var span = _tree.GetLineSpan(node.Span);
            return new ParsedMethodData(
                name, returnType, access, parameters, attributes,
                span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1);
        }

        private ParsedMethodData ParseConstructor(ConstructorDeclarationSyntax node)
        {
            var name = node.Identifier.Text;
            var access = GetAccessModifier(node.Modifiers);
            var attributes = ExtractAttributes(node.AttributeLists);

            var parameters = node.ParameterList.Parameters
                .Select(p => $"{p.Type} {p.Identifier.Text}".Trim())
                .ToList();

            var span = _tree.GetLineSpan(node.Span);
            return new ParsedMethodData(
                name, "Void", access, parameters, attributes,
                span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1);
        }

        private ParsedPropertyData ParseProperty(PropertyDeclarationSyntax node)
        {
            var name = node.Identifier.Text;
            var type = node.Type.ToString();
            var access = GetAccessModifier(node.Modifiers);
            var attributes = ExtractAttributes(node.AttributeLists);

            var span = _tree.GetLineSpan(node.Span);
            return new ParsedPropertyData(
                name, type, access, attributes,
                span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1);
        }

        private IEnumerable<ParsedFieldData> ParseFields(FieldDeclarationSyntax node)
        {
            var access = GetAccessModifier(node.Modifiers);
            var attributes = ExtractAttributes(node.AttributeLists);
            var type = node.Declaration.Type.ToString();
            var span = _tree.GetLineSpan(node.Span);

            return node.Declaration.Variables.Select(v => new ParsedFieldData(
                v.Identifier.Text, type, access, attributes,
                span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1));
        }

        private string? GetNamespace(SyntaxNode node)
        {
            var nsNode = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
            return nsNode?.Name.ToString();
        }

        private List<string> ExtractAttributes(SyntaxList<AttributeListSyntax> attributeLists)
        {
            return attributeLists
                .SelectMany(al => al.Attributes)
                .Select(a => a.Name.ToString())
                .ToList();
        }

        private string GetAccessModifier(SyntaxTokenList modifiers)
        {
            if (modifiers.Any(SyntaxKind.PublicKeyword)) return "public";
            if (modifiers.Any(SyntaxKind.InternalKeyword)) return "internal";
            if (modifiers.Any(SyntaxKind.ProtectedKeyword)) return "protected";
            if (modifiers.Any(SyntaxKind.PrivateKeyword)) return "private";
            return "private"; // default
        }
    }
}
