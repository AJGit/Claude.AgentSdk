using Claude.AgentSdk.Analyzers.Tests.Verifiers;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Claude.AgentSdk.Analyzers.Tests;

/// <summary>
///     Tests for the DangerousPermissionAnalyzer.
/// </summary>
public class DangerousPermissionAnalyzerTests
{
    private static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        return CSharpAnalyzerVerifier<DangerousPermissionAnalyzer>.VerifyAnalyzerAsync(source, expected);
    }

    private static Task VerifyNoDiagnosticsAsync(string source)
    {
        return CSharpAnalyzerVerifier<DangerousPermissionAnalyzer>.VerifyNoDiagnosticsAsync(source);
    }

    private static DiagnosticResult Diagnostic()
    {
        return CSharpAnalyzerVerifier<DangerousPermissionAnalyzer>.Diagnostic(DiagnosticDescriptors
            .DangerousPermissionSkip);
    }

    [Fact]
    public async Task DangerouslySkipPermissions_SetToTrue_ReportsDiagnostic()
    {
        string source = """
                        class TestClass
                        {
                            public bool DangerouslySkipPermissions { get; set; }

                            void Test()
                            {
                                DangerouslySkipPermissions = true;
                            }
                        }
                        """;

        await VerifyAnalyzerAsync(source, Diagnostic().WithSpan(7, 9, 7, 42));
    }

    [Fact]
    public async Task DangerouslySkipPermissions_SetToFalse_NoDiagnostic()
    {
        string source = """
                        class TestClass
                        {
                            public bool DangerouslySkipPermissions { get; set; }

                            void Test()
                            {
                                DangerouslySkipPermissions = false;
                            }
                        }
                        """;

        await VerifyNoDiagnosticsAsync(source);
    }

    [Fact]
    public async Task DangerouslySkipPermissions_MemberAccess_SetToTrue_ReportsDiagnostic()
    {
        string source = """
                        class Options
                        {
                            public bool DangerouslySkipPermissions { get; set; }
                        }

                        class TestClass
                        {
                            void Test()
                            {
                                var options = new Options();
                                options.DangerouslySkipPermissions = true;
                            }
                        }
                        """;

        await VerifyAnalyzerAsync(source, Diagnostic().WithSpan(11, 9, 11, 50));
    }

    [Fact]
    public async Task DangerouslySkipPermissions_MemberAccess_SetToFalse_NoDiagnostic()
    {
        string source = """
                        class Options
                        {
                            public bool DangerouslySkipPermissions { get; set; }
                        }

                        class TestClass
                        {
                            void Test()
                            {
                                var options = new Options();
                                options.DangerouslySkipPermissions = false;
                            }
                        }
                        """;

        await VerifyNoDiagnosticsAsync(source);
    }

    [Fact]
    public async Task OtherProperty_SetToTrue_NoDiagnostic()
    {
        string source = """
                        class TestClass
                        {
                            public bool SomeOtherProperty { get; set; }

                            void Test()
                            {
                                SomeOtherProperty = true;
                            }
                        }
                        """;

        await VerifyNoDiagnosticsAsync(source);
    }

    [Fact]
    public async Task DangerouslySkipAllPermissions_MethodCall_ReportsDiagnostic()
    {
        string source = """
                        class Builder
                        {
                            public Builder DangerouslySkipAllPermissions() => this;
                        }

                        class TestClass
                        {
                            void Test()
                            {
                                var builder = new Builder();
                                builder.DangerouslySkipAllPermissions();
                            }
                        }
                        """;

        await VerifyAnalyzerAsync(source, Diagnostic().WithLocation(11, 9));
    }

    [Fact]
    public async Task DangerouslySkipAllPermissions_FluentCall_ReportsDiagnostic()
    {
        string source = """
                        class Builder
                        {
                            public Builder WithModel(string model) => this;
                            public Builder DangerouslySkipAllPermissions() => this;
                            public object Build() => new object();
                        }

                        class TestClass
                        {
                            void Test()
                            {
                                new Builder()
                                    .WithModel("test")
                                    .DangerouslySkipAllPermissions()
                                    .Build();
                            }
                        }
                        """;

        await VerifyAnalyzerAsync(source, Diagnostic().WithLocation(12, 9));
    }

    [Fact]
    public async Task OtherMethod_NoDiagnostic()
    {
        string source = """
                        class Builder
                        {
                            public Builder SomeOtherMethod() => this;
                        }

                        class TestClass
                        {
                            void Test()
                            {
                                var builder = new Builder();
                                builder.SomeOtherMethod();
                            }
                        }
                        """;

        await VerifyNoDiagnosticsAsync(source);
    }

    [Fact]
    public async Task DangerouslySkipPermissions_WithVariable_NoDiagnostic()
    {
        // When the value is from a variable, we can't statically determine if it's true
        string source = """
                        class TestClass
                        {
                            public bool DangerouslySkipPermissions { get; set; }

                            void Test(bool value)
                            {
                                DangerouslySkipPermissions = value;
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
