using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Claude.AgentSdk.Analyzers.Tests.Verifiers;

/// <summary>
///     Helper for testing C# diagnostic analyzers.
/// </summary>
/// <typeparam name="TAnalyzer">The type of the analyzer to test.</typeparam>
#pragma warning disable CA1000 // Do not declare static members on generic types - required for analyzer test pattern
public static class CSharpAnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    /// <summary>
    ///     Verifies that the analyzer produces no diagnostics for the given source code.
    /// </summary>
    public static async Task VerifyNoDiagnosticsAsync(string source)
    {
        var test = new Test
        {
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>
    ///     Verifies that the analyzer produces the expected diagnostics.
    /// </summary>
    public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new Test
        {
            TestCode = source
        };

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>
    ///     Creates a diagnostic result for the given descriptor.
    /// </summary>
    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor) =>
        CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic(descriptor);

    /// <summary>
    ///     Creates a diagnostic result for the given diagnostic ID.
    /// </summary>
    public static DiagnosticResult Diagnostic(string diagnosticId) =>
        CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic(diagnosticId);
#pragma warning restore CA1000

    /// <summary>
    ///     Test class configured for Claude Agent SDK analyzers.
    /// </summary>
    private sealed class Test : CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
    {
        public Test()
        {
            // Use .NET 9 reference assemblies
            ReferenceAssemblies = new ReferenceAssemblies(
                "net9.0",
                new PackageIdentity("Microsoft.NETCore.App.Ref", "9.0.0"),
                Path.Combine("ref", "net9.0"));
        }

        protected override CompilationOptions CreateCompilationOptions()
        {
            var options = base.CreateCompilationOptions();
            return options.WithSpecificDiagnosticOptions(
                options.SpecificDiagnosticOptions.SetItem("CS8019", ReportDiagnostic.Suppress));
        }
    }
}
