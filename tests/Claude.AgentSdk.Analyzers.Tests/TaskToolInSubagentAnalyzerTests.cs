using Claude.AgentSdk.Analyzers.Tests.Verifiers;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Claude.AgentSdk.Analyzers.Tests;

/// <summary>
///     Tests for the TaskToolInSubagentAnalyzer.
/// </summary>
public class TaskToolInSubagentAnalyzerTests
{
    private static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        => CSharpAnalyzerVerifier<TaskToolInSubagentAnalyzer>.VerifyAnalyzerAsync(source, expected);

    private static Task VerifyNoDiagnosticsAsync(string source)
        => CSharpAnalyzerVerifier<TaskToolInSubagentAnalyzer>.VerifyNoDiagnosticsAsync(source);

    private static DiagnosticResult Diagnostic()
        => CSharpAnalyzerVerifier<TaskToolInSubagentAnalyzer>.Diagnostic(DiagnosticDescriptors.TaskToolInSubagent);

    #region String Literal "Task" Tests

    [Fact]
    public async Task AgentDefinition_WithTaskStringLiteral_InArray_ReportsDiagnostic()
    {
        var source = """
            using System.Collections.Generic;

            class AgentDefinition
            {
                public string[] Tools { get; set; }
            }

            class TestClass
            {
                void Test()
                {
                    var agent = new AgentDefinition
                    {
                        Tools = new[] { "Read", "Task", "Write" }
                    };
                }
            }
            """;

        await VerifyAnalyzerAsync(source, Diagnostic().WithLocation(14, 37));
    }

    [Fact]
    public async Task AgentDefinition_WithoutTask_NoDiagnostic()
    {
        var source = """
            using System.Collections.Generic;

            class AgentDefinition
            {
                public string[] Tools { get; set; }
            }

            class TestClass
            {
                void Test()
                {
                    var agent = new AgentDefinition
                    {
                        Tools = new[] { "Read", "Write", "Bash" }
                    };
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    [Fact]
    public async Task AgentDefinition_WithTaskOnly_ReportsDiagnostic()
    {
        var source = """
            class AgentDefinition
            {
                public string[] Tools { get; set; }
            }

            class TestClass
            {
                void Test()
                {
                    var agent = new AgentDefinition
                    {
                        Tools = new[] { "Task" }
                    };
                }
            }
            """;

        await VerifyAnalyzerAsync(source, Diagnostic().WithLocation(12, 29));
    }

    #endregion

    #region Collection Expression Tests

    [Fact]
    public async Task AgentDefinition_WithTaskInCollectionExpression_ReportsDiagnostic()
    {
        var source = """
            class AgentDefinition
            {
                public string[] Tools { get; set; }
            }

            class TestClass
            {
                void Test()
                {
                    var agent = new AgentDefinition
                    {
                        Tools = ["Read", "Task", "Write"]
                    };
                }
            }
            """;

        await VerifyAnalyzerAsync(source, Diagnostic().WithLocation(12, 30));
    }

    #endregion

    #region Builder Pattern Tests

    [Fact]
    public async Task AgentDefinitionBuilder_WithToolsContainingTask_ReportsDiagnostic()
    {
        var source = """
            class AgentDefinitionBuilder
            {
                public AgentDefinitionBuilder WithTools(params string[] tools) => this;
            }

            class TestClass
            {
                void Test()
                {
                    var builder = new AgentDefinitionBuilder();
                    builder.WithTools("Read", "Task");
                }
            }
            """;

        await VerifyAnalyzerAsync(source, Diagnostic().WithLocation(11, 35));
    }

    [Fact]
    public async Task AgentDefinitionBuilder_AddToolsContainingTask_ReportsDiagnostic()
    {
        var source = """
            class AgentDefinitionBuilder
            {
                public AgentDefinitionBuilder AddTools(params string[] tools) => this;
            }

            class TestClass
            {
                void Test()
                {
                    var builder = new AgentDefinitionBuilder();
                    builder.AddTools("Task");
                }
            }
            """;

        await VerifyAnalyzerAsync(source, Diagnostic().WithLocation(11, 26));
    }

    [Fact]
    public async Task AgentDefinitionBuilder_WithToolsWithoutTask_NoDiagnostic()
    {
        var source = """
            class AgentDefinitionBuilder
            {
                public AgentDefinitionBuilder WithTools(params string[] tools) => this;
            }

            class TestClass
            {
                void Test()
                {
                    var builder = new AgentDefinitionBuilder();
                    builder.WithTools("Read", "Write");
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    #endregion

    #region ToolName.Task Tests

    [Fact]
    public async Task AgentDefinition_WithToolNameTask_ReportsDiagnostic()
    {
        var source = """
            struct ToolName
            {
                public static ToolName Task => default;
                public static ToolName Read => default;
            }

            class AgentDefinition
            {
                public ToolName[] Tools { get; set; }
            }

            class TestClass
            {
                void Test()
                {
                    var agent = new AgentDefinition
                    {
                        Tools = new[] { ToolName.Read, ToolName.Task }
                    };
                }
            }
            """;

        await VerifyAnalyzerAsync(source, Diagnostic().WithLocation(18, 44));
    }

    [Fact]
    public async Task AgentDefinitionBuilder_WithToolNameTask_ReportsDiagnostic()
    {
        var source = """
            struct ToolName
            {
                public static ToolName Task => default;
                public static ToolName Read => default;
            }

            class AgentDefinitionBuilder
            {
                public AgentDefinitionBuilder WithTools(params ToolName[] tools) => this;
            }

            class TestClass
            {
                void Test()
                {
                    var builder = new AgentDefinitionBuilder();
                    builder.WithTools(ToolName.Task);
                }
            }
            """;

        await VerifyAnalyzerAsync(source, Diagnostic().WithLocation(17, 27));
    }

    #endregion

    #region Non-AgentDefinition Tests

    [Fact]
    public async Task OtherClass_WithToolsContainingTask_NoDiagnostic()
    {
        var source = """
            class SomeOtherClass
            {
                public string[] Tools { get; set; }
            }

            class TestClass
            {
                void Test()
                {
                    var other = new SomeOtherClass
                    {
                        Tools = new[] { "Task" }
                    };
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    [Fact]
    public async Task OtherBuilder_WithToolsContainingTask_NoDiagnostic()
    {
        var source = """
            class SomeOtherBuilder
            {
                public SomeOtherBuilder WithTools(params string[] tools) => this;
            }

            class TestClass
            {
                void Test()
                {
                    var builder = new SomeOtherBuilder();
                    builder.WithTools("Task");
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task AgentDefinition_NoInitializer_NoDiagnostic()
    {
        var source = """
            class AgentDefinition
            {
                public string[] Tools { get; set; }
            }

            class TestClass
            {
                void Test()
                {
                    var agent = new AgentDefinition();
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
