using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Claude.AgentSdk.Analyzers;

/// <summary>
///     Analyzer that detects duplicate tools in AllowedTools/DisallowedTools.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateToolsAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.DuplicateAllowedTools,
            DiagnosticDescriptors.ConflictingToolPermissions);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        AssignmentExpressionSyntax assignment = (AssignmentExpressionSyntax)context.Node;

        // Check if assigning to AllowedTools or DisallowedTools
        string? propertyName = null;
        if (assignment.Left is IdentifierNameSyntax identifier)
        {
            propertyName = identifier.Identifier.Text;
        }
        else if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
        {
            propertyName = memberAccess.Name.Identifier.Text;
        }

        if (propertyName != "AllowedTools" && propertyName != "DisallowedTools")
        {
            return;
        }

        // Extract tool names from the assignment
        List<(string Name, Location Location)> toolNames = ExtractToolNames(context, assignment.Right);

        // Check for duplicates
        HashSet<string> seen = [];
        foreach ((string name, Location location) in toolNames)
        {
            if (!seen.Add(name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateAllowedTools,
                    location,
                    name));
            }
        }
    }

    private static List<(string Name, Location Location)> ExtractToolNames(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression)
    {
        List<(string, Location)> result = [];

        // Handle array creation
        if (expression is ArrayCreationExpressionSyntax { Initializer: not null } arrayCreation)
        {
            foreach (ExpressionSyntax element in arrayCreation.Initializer.Expressions)
            {
                if (TryGetToolName(context, element, out string name))
                {
                    result.Add((name, element.GetLocation()));
                }
            }
        }
        // Handle implicit array
        else if (expression is ImplicitArrayCreationExpressionSyntax implicitArray)
        {
            foreach (ExpressionSyntax element in implicitArray.Initializer.Expressions)
            {
                if (TryGetToolName(context, element, out string name))
                {
                    result.Add((name, element.GetLocation()));
                }
            }
        }
        // Handle collection expression
        else if (expression is CollectionExpressionSyntax collection)
        {
            foreach (CollectionElementSyntax element in collection.Elements)
            {
                if (element is ExpressionElementSyntax exprElement &&
                    TryGetToolName(context, exprElement.Expression, out string name))
                {
                    result.Add((name, exprElement.GetLocation()));
                }
            }
        }

        return result;
    }

    private static bool TryGetToolName(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax expression,
        out string name)
    {
        name = string.Empty;

        // String literal
        if (expression is LiteralExpressionSyntax literal &&
            literal.Kind() == SyntaxKind.StringLiteralExpression)
        {
            name = literal.Token.ValueText;
            return true;
        }

        // ToolName.X
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            TypeInfo typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression);
            if (typeInfo.Type?.Name == "ToolName")
            {
                name = memberAccess.Name.Identifier.Text;
                return true;
            }
        }

        return false;
    }
}
