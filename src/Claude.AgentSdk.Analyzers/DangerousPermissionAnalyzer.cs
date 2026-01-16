using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Claude.AgentSdk.Analyzers;

/// <summary>
///     Analyzer that warns when DangerouslySkipPermissions is enabled.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DangerousPermissionAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.DangerousPermissionSkip);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Check if assigning to DangerouslySkipPermissions
        string? propertyName = null;
        if (assignment.Left is IdentifierNameSyntax identifier)
        {
            propertyName = identifier.Identifier.Text;
        }
        else if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
        {
            propertyName = memberAccess.Name.Identifier.Text;
        }

        if (propertyName != "DangerouslySkipPermissions")
            return;

        // Check if value is true
        if (assignment.Right is LiteralExpressionSyntax literal &&
            literal.Kind() == SyntaxKind.TrueLiteralExpression)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DangerousPermissionSkip,
                assignment.GetLocation()));
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check for builder method calls like .DangerouslySkipAllPermissions()
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        if (memberAccess.Name.Identifier.Text == "DangerouslySkipAllPermissions")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DangerousPermissionSkip,
                invocation.GetLocation()));
        }
    }
}
