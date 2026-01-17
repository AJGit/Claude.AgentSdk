using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Claude.AgentSdk.Analyzers;

/// <summary>
///     Analyzer that detects invalid configuration values.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvalidConfigurationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.InvalidMaxTurns,
            DiagnosticDescriptors.InvalidMaxBudget,
            DiagnosticDescriptors.EmptySystemPrompt);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        AssignmentExpressionSyntax assignment = (AssignmentExpressionSyntax)context.Node;

        // Get property name
        string? propertyName = null;
        if (assignment.Left is IdentifierNameSyntax identifier)
        {
            propertyName = identifier.Identifier.Text;
        }
        else if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
        {
            propertyName = memberAccess.Name.Identifier.Text;
        }

        if (propertyName is null)
        {
            return;
        }

        switch (propertyName)
        {
            case "MaxTurns":
                AnalyzeMaxTurns(context, assignment.Right);
                break;
            case "MaxBudgetUsd":
                AnalyzeMaxBudget(context, assignment.Right);
                break;
            case "SystemPrompt" when assignment.Right is LiteralExpressionSyntax literal:
                AnalyzeSystemPrompt(context, literal);
                break;
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        string methodName = memberAccess.Name.Identifier.Text;

        switch (methodName)
        {
            case "WithMaxTurns" when invocation.ArgumentList.Arguments.Count > 0:
                AnalyzeMaxTurns(context, invocation.ArgumentList.Arguments[0].Expression);
                break;
            case "WithMaxBudget" when invocation.ArgumentList.Arguments.Count > 0:
                AnalyzeMaxBudget(context, invocation.ArgumentList.Arguments[0].Expression);
                break;
            case "WithSystemPrompt" when invocation.ArgumentList.Arguments.Count > 0:
                if (invocation.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax literal)
                {
                    AnalyzeSystemPrompt(context, literal);
                }

                break;
        }
    }

    private static void AnalyzeMaxTurns(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        // Handle both literal values and unary expressions (e.g., -5)
        Optional<object?> constantValue = context.SemanticModel.GetConstantValue(expression);
        if (!constantValue.HasValue)
        {
            return;
        }

        if (constantValue.Value is int intValue and <= 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidMaxTurns,
                expression.GetLocation(),
                intValue));
        }
    }

    private static void AnalyzeMaxBudget(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
    {
        // Handle both literal values and unary expressions (e.g., -1.5)
        Optional<object?> constantValue = context.SemanticModel.GetConstantValue(expression);
        if (!constantValue.HasValue)
        {
            return;
        }

        double? value = constantValue.Value switch
        {
            double d => d,
            float f => (double)f,
            int i => (double)i,
            _ => null
        };

        if (value is <= 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidMaxBudget,
                expression.GetLocation(),
                value.Value));
        }
    }

    private static void AnalyzeSystemPrompt(SyntaxNodeAnalysisContext context, LiteralExpressionSyntax literal)
    {
        if (literal.Kind() != SyntaxKind.StringLiteralExpression)
        {
            return;
        }

        string value = literal.Token.ValueText;
        if (string.IsNullOrWhiteSpace(value))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.EmptySystemPrompt,
                literal.GetLocation()));
        }
    }
}
