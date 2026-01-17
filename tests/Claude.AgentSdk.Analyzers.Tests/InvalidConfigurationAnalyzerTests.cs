using Claude.AgentSdk.Analyzers.Tests.Verifiers;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Claude.AgentSdk.Analyzers.Tests;

/// <summary>
///     Tests for the InvalidConfigurationAnalyzer.
/// </summary>
public class InvalidConfigurationAnalyzerTests
{
    private static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        => CSharpAnalyzerVerifier<InvalidConfigurationAnalyzer>.VerifyAnalyzerAsync(source, expected);

    private static Task VerifyNoDiagnosticsAsync(string source)
        => CSharpAnalyzerVerifier<InvalidConfigurationAnalyzer>.VerifyNoDiagnosticsAsync(source);

    private static DiagnosticResult MaxTurnsDiagnostic(int value)
        => CSharpAnalyzerVerifier<InvalidConfigurationAnalyzer>
            .Diagnostic(DiagnosticDescriptors.InvalidMaxTurns)
            .WithArguments(value);

    private static DiagnosticResult MaxBudgetDiagnostic(double value)
        => CSharpAnalyzerVerifier<InvalidConfigurationAnalyzer>
            .Diagnostic(DiagnosticDescriptors.InvalidMaxBudget)
            .WithArguments(value);

    private static DiagnosticResult EmptySystemPromptDiagnostic()
        => CSharpAnalyzerVerifier<InvalidConfigurationAnalyzer>
            .Diagnostic(DiagnosticDescriptors.EmptySystemPrompt);

    #region MaxTurns Tests

    [Fact]
    public async Task MaxTurns_SetToZero_ReportsDiagnostic()
    {
        var source = """
            class TestClass
            {
                public int MaxTurns { get; set; }

                void Test()
                {
                    MaxTurns = 0;
                }
            }
            """;

        await VerifyAnalyzerAsync(source, MaxTurnsDiagnostic(0).WithLocation(7, 20));
    }

    [Fact]
    public async Task MaxTurns_SetToNegative_ReportsDiagnostic()
    {
        var source = """
            class TestClass
            {
                public int MaxTurns { get; set; }

                void Test()
                {
                    MaxTurns = -5;
                }
            }
            """;

        await VerifyAnalyzerAsync(source, MaxTurnsDiagnostic(-5).WithLocation(7, 20));
    }

    [Fact]
    public async Task MaxTurns_SetToPositive_NoDiagnostic()
    {
        var source = """
            class TestClass
            {
                public int MaxTurns { get; set; }

                void Test()
                {
                    MaxTurns = 10;
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    [Fact]
    public async Task MaxTurns_MemberAccess_SetToZero_ReportsDiagnostic()
    {
        var source = """
            class Options
            {
                public int MaxTurns { get; set; }
            }

            class TestClass
            {
                void Test()
                {
                    var options = new Options();
                    options.MaxTurns = 0;
                }
            }
            """;

        await VerifyAnalyzerAsync(source, MaxTurnsDiagnostic(0).WithLocation(11, 28));
    }

    [Fact]
    public async Task WithMaxTurns_SetToZero_ReportsDiagnostic()
    {
        var source = """
            class Builder
            {
                public Builder WithMaxTurns(int turns) => this;
            }

            class TestClass
            {
                void Test()
                {
                    var builder = new Builder();
                    builder.WithMaxTurns(0);
                }
            }
            """;

        await VerifyAnalyzerAsync(source, MaxTurnsDiagnostic(0).WithLocation(11, 30));
    }

    [Fact]
    public async Task WithMaxTurns_SetToPositive_NoDiagnostic()
    {
        var source = """
            class Builder
            {
                public Builder WithMaxTurns(int turns) => this;
            }

            class TestClass
            {
                void Test()
                {
                    var builder = new Builder();
                    builder.WithMaxTurns(100);
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    #endregion

    #region MaxBudgetUsd Tests

    [Fact]
    public async Task MaxBudgetUsd_SetToZero_ReportsDiagnostic()
    {
        var source = """
            class TestClass
            {
                public double MaxBudgetUsd { get; set; }

                void Test()
                {
                    MaxBudgetUsd = 0.0;
                }
            }
            """;

        await VerifyAnalyzerAsync(source, MaxBudgetDiagnostic(0.0).WithLocation(7, 24));
    }

    [Fact]
    public async Task MaxBudgetUsd_SetToNegative_ReportsDiagnostic()
    {
        var source = """
            class TestClass
            {
                public double MaxBudgetUsd { get; set; }

                void Test()
                {
                    MaxBudgetUsd = -1.5;
                }
            }
            """;

        await VerifyAnalyzerAsync(source, MaxBudgetDiagnostic(-1.5).WithLocation(7, 24));
    }

    [Fact]
    public async Task MaxBudgetUsd_SetToPositive_NoDiagnostic()
    {
        var source = """
            class TestClass
            {
                public double MaxBudgetUsd { get; set; }

                void Test()
                {
                    MaxBudgetUsd = 5.0;
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    [Fact]
    public async Task MaxBudgetUsd_SetToZeroInt_ReportsDiagnostic()
    {
        var source = """
            class TestClass
            {
                public double MaxBudgetUsd { get; set; }

                void Test()
                {
                    MaxBudgetUsd = 0;
                }
            }
            """;

        await VerifyAnalyzerAsync(source, MaxBudgetDiagnostic(0).WithLocation(7, 24));
    }

    [Fact]
    public async Task WithMaxBudget_SetToZero_ReportsDiagnostic()
    {
        var source = """
            class Builder
            {
                public Builder WithMaxBudget(double budget) => this;
            }

            class TestClass
            {
                void Test()
                {
                    var builder = new Builder();
                    builder.WithMaxBudget(0.0);
                }
            }
            """;

        await VerifyAnalyzerAsync(source, MaxBudgetDiagnostic(0.0).WithLocation(11, 31));
    }

    [Fact]
    public async Task WithMaxBudget_SetToPositive_NoDiagnostic()
    {
        var source = """
            class Builder
            {
                public Builder WithMaxBudget(double budget) => this;
            }

            class TestClass
            {
                void Test()
                {
                    var builder = new Builder();
                    builder.WithMaxBudget(10.0);
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    #endregion

    #region SystemPrompt Tests

    [Fact]
    public async Task SystemPrompt_Empty_ReportsDiagnostic()
    {
        var source = """
            class TestClass
            {
                public string SystemPrompt { get; set; }

                void Test()
                {
                    SystemPrompt = "";
                }
            }
            """;

        await VerifyAnalyzerAsync(source, EmptySystemPromptDiagnostic().WithLocation(7, 24));
    }

    [Fact]
    public async Task SystemPrompt_Whitespace_ReportsDiagnostic()
    {
        var source = """
            class TestClass
            {
                public string SystemPrompt { get; set; }

                void Test()
                {
                    SystemPrompt = "   ";
                }
            }
            """;

        await VerifyAnalyzerAsync(source, EmptySystemPromptDiagnostic().WithLocation(7, 24));
    }

    [Fact]
    public async Task SystemPrompt_NonEmpty_NoDiagnostic()
    {
        var source = """
            class TestClass
            {
                public string SystemPrompt { get; set; }

                void Test()
                {
                    SystemPrompt = "You are a helpful assistant.";
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    [Fact]
    public async Task WithSystemPrompt_Empty_ReportsDiagnostic()
    {
        var source = """
            class Builder
            {
                public Builder WithSystemPrompt(string prompt) => this;
            }

            class TestClass
            {
                void Test()
                {
                    var builder = new Builder();
                    builder.WithSystemPrompt("");
                }
            }
            """;

        await VerifyAnalyzerAsync(source, EmptySystemPromptDiagnostic().WithLocation(11, 34));
    }

    [Fact]
    public async Task WithSystemPrompt_NonEmpty_NoDiagnostic()
    {
        var source = """
            class Builder
            {
                public Builder WithSystemPrompt(string prompt) => this;
            }

            class TestClass
            {
                void Test()
                {
                    var builder = new Builder();
                    builder.WithSystemPrompt("You are a helpful assistant.");
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    #endregion

    #region Other Property Tests

    [Fact]
    public async Task OtherIntProperty_SetToZero_NoDiagnostic()
    {
        var source = """
            class TestClass
            {
                public int SomeOtherProperty { get; set; }

                void Test()
                {
                    SomeOtherProperty = 0;
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    [Fact]
    public async Task OtherStringProperty_Empty_NoDiagnostic()
    {
        var source = """
            class TestClass
            {
                public string SomeOtherProperty { get; set; }

                void Test()
                {
                    SomeOtherProperty = "";
                }
            }
            """;

        await VerifyNoDiagnosticsAsync(source);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task MaxTurns_FromVariable_NoDiagnostic()
    {
        // When the value is from a variable, we can't statically determine the value
        var source = """
            class TestClass
            {
                public int MaxTurns { get; set; }

                void Test(int value)
                {
                    MaxTurns = value;
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
