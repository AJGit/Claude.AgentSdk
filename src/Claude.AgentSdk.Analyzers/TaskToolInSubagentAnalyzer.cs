using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Claude.AgentSdk.Analyzers;

/// <summary>
///     Analyzer that detects when Task tool is included in subagent tools.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TaskToolInSubagentAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.TaskToolInSubagent);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        ObjectCreationExpressionSyntax objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        // Check if creating AgentDefinition
        ITypeSymbol? typeSymbol = context.SemanticModel.GetTypeInfo(objectCreation).Type;
        if (typeSymbol?.Name != "AgentDefinition")
        {
            return;
        }

        // Check initializer for Tools property containing "Task"
        if (objectCreation.Initializer is null)
        {
            return;
        }

        foreach (ExpressionSyntax expression in objectCreation.Initializer.Expressions)
        {
            if (expression is not AssignmentExpressionSyntax assignment)
            {
                continue;
            }

            if (assignment.Left is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            if (identifier.Identifier.Text != "Tools")
            {
                continue;
            }

            // Check if Tools contains "Task"
            CheckForTaskTool(context, assignment.Right);
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;

        // Check for AgentDefinitionBuilder.WithTools() calls
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (memberAccess.Name.Identifier.Text != "WithTools" &&
            memberAccess.Name.Identifier.Text != "AddTools")
        {
            return;
        }

        // Verify it's on AgentDefinitionBuilder
        TypeInfo typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression);
        if (typeInfo.Type?.Name != "AgentDefinitionBuilder")
        {
            return;
        }

        // Check arguments for "Task"
        foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
        {
            CheckForTaskTool(context, argument.Expression);
        }
    }

    private static void CheckForTaskTool(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        // Check for string literal "Task"
        if (expression is LiteralExpressionSyntax { Token.ValueText: "Task" } literal)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.TaskToolInSubagent,
                literal.GetLocation()));
            return;
        }

        // Check for ToolName.Task
        if (expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Task" } memberAccess)
        {
            TypeInfo typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression);
            if (typeInfo.Type?.Name == "ToolName")
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TaskToolInSubagent,
                    memberAccess.GetLocation()));
            }

            return;
        }

        // Check arrays and collections
        if (expression is ArrayCreationExpressionSyntax { Initializer: not null } arrayCreation)
        {
            foreach (ExpressionSyntax element in arrayCreation.Initializer.Expressions)
            {
                CheckForTaskTool(context, element);
            }

            return;
        }

        if (expression is ImplicitArrayCreationExpressionSyntax implicitArray)
        {
            foreach (ExpressionSyntax element in implicitArray.Initializer.Expressions)
            {
                CheckForTaskTool(context, element);
            }

            return;
        }

        if (expression is CollectionExpressionSyntax collection)
        {
            foreach (CollectionElementSyntax element in collection.Elements)
            {
                if (element is ExpressionElementSyntax exprElement)
                {
                    CheckForTaskTool(context, exprElement.Expression);
                }
            }
        }
    }
}
