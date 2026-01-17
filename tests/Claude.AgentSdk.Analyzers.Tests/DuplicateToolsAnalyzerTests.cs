using Claude.AgentSdk.Analyzers.Tests.Verifiers;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Claude.AgentSdk.Analyzers.Tests;

/// <summary>
///     Tests for the DuplicateToolsAnalyzer.
/// </summary>
public class DuplicateToolsAnalyzerTests
{
    private static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        => CSharpAnalyzerVerifier<DuplicateToolsAnalyzer>.VerifyAnalyzerAsync(source, expected);

    private static Task VerifyNoDiagnosticsAsync(string source)
        => CSharpAnalyzerVerifier<DuplicateToolsAnalyzer>.VerifyNoDiagnosticsAsync(source);

    private static DiagnosticResult DuplicateDiagnostic(string toolName)
        => CSharpAnalyzerVerifier<DuplicateToolsAnalyzer>
            .Diagnostic(DiagnosticDescriptors.DuplicateAllowedTools)
            .WithArguments(toolName);

    #region AllowedTools Duplicate Detection Tests

    [Fact]
    public async Task AllowedTools_WithDuplicateStringLiterals_ReportsDiagnostic()
    {
        var source = """
            class TestClass
            {
                public string[] AllowedTools { get; set; }

                void Test()
                {
                    AllowedTools = new[] { "Read", "Write", "Read" };
                }
            }
            """;

        await VerifyAnalyzerAsync(source, DuplicateDiagnostic("Read").WithLocation(7, 49));
    }

    [Fact]
    public async Task AllowedTools_WithMultipleDuplicates_ReportsMultipleDiagnostics()
    {
        var source = """
            class TestClass
            {
                public string[] AllowedTools { get; set; }

                void Test()
                {
                    AllowedTools = new[] { "Read", "Read", "Write", "Write" };
                }
            }
            """;

        await VerifyAnalyzerAsync(source,
            DuplicateDiagnostic("Read").WithLocation(7, 40),
            DuplicateDiagnostic("Write").WithLocation(7, 57));
    }

    [Fact]
    public async Task AllowedTools_NoDuplicates_NoDiagnostic()
    {
        var source = """
            class TestClass
            {
                public string[] AllowedTools { get; set; }

                void Test()
                {
                    AllowedTools = new[] { "Read", "Write", "Bash" };
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    [Fact]
    public async Task AllowedTools_MemberAccess_WithDuplicates_ReportsDiagnostic()
    {
        var source = """
            class Options
            {
                public string[] AllowedTools { get; set; }
            }

            class TestClass
            {
                void Test()
                {
                    var options = new Options();
                    options.AllowedTools = new[] { "Read", "Read" };
                }
            }
            """;

        await VerifyAnalyzerAsync(source, DuplicateDiagnostic("Read").WithLocation(11, 48));
    }

    #endregion

    #region DisallowedTools Duplicate Detection Tests

    [Fact]
    public async Task DisallowedTools_WithDuplicateStringLiterals_ReportsDiagnostic()
    {
        var source = """
            class TestClass
            {
                public string[] DisallowedTools { get; set; }

                void Test()
                {
                    DisallowedTools = new[] { "Bash", "Bash" };
                }
            }
            """;

        await VerifyAnalyzerAsync(source, DuplicateDiagnostic("Bash").WithLocation(7, 43));
    }

    #endregion

    #region ToolName Duplicate Detection Tests

    [Fact]
    public async Task AllowedTools_WithDuplicateToolNames_ReportsDiagnostic()
    {
        var source = """
            struct ToolName
            {
                public static ToolName Read => default;
                public static ToolName Write => default;
            }

            class TestClass
            {
                public ToolName[] AllowedTools { get; set; }

                void Test()
                {
                    AllowedTools = new[] { ToolName.Read, ToolName.Write, ToolName.Read };
                }
            }
            """;

        await VerifyAnalyzerAsync(source, DuplicateDiagnostic("Read").WithLocation(13, 63));
    }

    #endregion

    #region Collection Expression Tests

    [Fact]
    public async Task AllowedTools_CollectionExpression_WithDuplicates_ReportsDiagnostic()
    {
        var source = """
            class TestClass
            {
                public string[] AllowedTools { get; set; }

                void Test()
                {
                    AllowedTools = ["Read", "Write", "Read"];
                }
            }
            """;

        await VerifyAnalyzerAsync(source, DuplicateDiagnostic("Read").WithLocation(7, 42));
    }

    #endregion

    #region Implicit Array Tests

    [Fact]
    public async Task AllowedTools_ImplicitArray_WithDuplicates_ReportsDiagnostic()
    {
        var source = """
            class TestClass
            {
                public string[] AllowedTools { get; set; }

                void Test()
                {
                    AllowedTools = new[] { "Read", "Read" };
                }
            }
            """;

        await VerifyAnalyzerAsync(source, DuplicateDiagnostic("Read").WithLocation(7, 40));
    }

    #endregion

    #region Other Property Tests

    [Fact]
    public async Task OtherProperty_WithDuplicates_NoDiagnostic()
    {
        var source = """
            class TestClass
            {
                public string[] SomeOtherProperty { get; set; }

                void Test()
                {
                    SomeOtherProperty = new[] { "Read", "Read" };
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task AllowedTools_EmptyArray_NoDiagnostic()
    {
        var source = """
            class TestClass
            {
                public string[] AllowedTools { get; set; }

                void Test()
                {
                    AllowedTools = new string[] { };
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    [Fact]
    public async Task AllowedTools_SingleElement_NoDiagnostic()
    {
        var source = """
            class TestClass
            {
                public string[] AllowedTools { get; set; }

                void Test()
                {
                    AllowedTools = new[] { "Read" };
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    [Fact]
    public async Task EmptyCode_NoDiagnostic()
    {
        var source = """
            class TestClass
            {
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    #endregion
}
