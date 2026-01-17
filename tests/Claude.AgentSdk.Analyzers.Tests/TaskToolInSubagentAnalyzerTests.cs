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
    {
        return CSharpAnalyzerVerifier<TaskToolInSubagentAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    private static Task VerifyNoDiagnosticsAsync(string source)
    {
        return CSharpAnalyzerVerifier<TaskToolInSubagentAnalyzer>.VerifyNoDiagnosticsAsync(source);
    }

    private static DiagnosticResult Diagnostic()
    {
        return CSharpAnalyzerVerifier<TaskToolInSubagentAnalyzer>.Diagnostic(DiagnosticDescriptors.TaskToolInSubagent);
    }

    [Fact]
    public async Task AgentDefinition_WithTaskInCollectionExpression_ReportsDiagnostic()
    {
        string source = """
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

    [Fact]
    public async Task AgentDefinition_WithTaskStringLiteral_InArray_ReportsDiagnostic()
    {
        string source = """
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
        string source = """
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
        string source = """
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

    [Fact]
    public async Task AgentDefinitionBuilder_WithToolsContainingTask_ReportsDiagnostic()
    {
        string source = """
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
        string source = """
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
        string source = """
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

    [Fact]
    public async Task AgentDefinition_WithToolNameTask_ReportsDiagnostic()
    {
        string source = """
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
        string source = """
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

    [Fact]
    public async Task OtherClass_WithToolsContainingTask_NoDiagnostic()
    {
        string source = """
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
        string source = """
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

    [Fact]
    public async Task AgentDefinition_NoInitializer_NoDiagnostic()
    {
        string source = """
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
        string source = """
                        class TestClass
                        {
                        }
                        """;

        await VerifyNoDiagnosticsAsync(source);
    }
}
