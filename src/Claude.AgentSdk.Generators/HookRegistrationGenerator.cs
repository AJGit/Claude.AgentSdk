using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Claude.AgentSdk.Generators;

/// <summary>
///     Source generator that creates compile-time hook registration code for classes
///     marked with [GenerateHookRegistration].
/// </summary>
[Generator]
public sealed class HookRegistrationGenerator : IIncrementalGenerator
{
    private const string GenerateHookRegistrationAttribute =
        "Claude.AgentSdk.Attributes.GenerateHookRegistrationAttribute";

    private const string HookHandlerAttribute = "Claude.AgentSdk.Attributes.HookHandlerAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all classes with [GenerateHookRegistration]
        IncrementalValuesProvider<ClassDeclarationSyntax?> classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (s, _) => IsCandidateClass(s),
                static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        // Combine with compilation
        IncrementalValueProvider<(Compilation Left, ImmutableArray<ClassDeclarationSyntax?> Right)>
            compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        // Generate source
        context.RegisterSourceOutput(compilationAndClasses,
            static (spc, source) => Execute(source.Left, source.Right!, spc));
    }

    private static bool IsCandidateClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };
    }

    private static ClassDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        ClassDeclarationSyntax classDeclaration = (ClassDeclarationSyntax)context.Node;

        foreach (AttributeListSyntax attributeList in classDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                ISymbol? symbol = context.SemanticModel.GetSymbolInfo(attribute).Symbol;
                if (symbol is IMethodSymbol methodSymbol)
                {
                    string attributeType = methodSymbol.ContainingType.ToDisplayString();
                    if (attributeType == GenerateHookRegistrationAttribute)
                    {
                        return classDeclaration;
                    }
                }
            }
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax?> classes,
        SourceProductionContext context)
    {
        if (classes.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (ClassDeclarationSyntax? classDeclaration in classes.Distinct())
        {
            if (classDeclaration is null)
            {
                continue;
            }

            SemanticModel semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
            INamedTypeSymbol? classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

            if (classSymbol is null)
            {
                continue;
            }

            List<HookMethodInfo> hookMethods = GetHookMethods(classSymbol, compilation);
            if (hookMethods.Count > 0)
            {
                string source = GenerateHookRegistrationExtension(classSymbol, hookMethods);
                context.AddSource($"{classSymbol.Name}HookRegistrationExtensions.g.cs",
                    SourceText.From(source, Encoding.UTF8));
            }
        }
    }

    private static List<HookMethodInfo> GetHookMethods(INamedTypeSymbol classSymbol, Compilation compilation)
    {
        List<HookMethodInfo> methods = [];
        INamedTypeSymbol? hookHandlerAttrSymbol = compilation.GetTypeByMetadataName(HookHandlerAttribute);

        if (hookHandlerAttrSymbol is null)
        {
            return methods;
        }

        foreach (ISymbol? member in classSymbol.GetMembers())
        {
            if (member is not IMethodSymbol methodSymbol)
            {
                continue;
            }

            List<AttributeData> hookAttributes = methodSymbol.GetAttributes()
                .Where(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, hookHandlerAttrSymbol))
                .ToList();

            if (hookAttributes.Count == 0)
            {
                continue;
            }

            foreach (AttributeData? attr in hookAttributes)
            {
                // Get the HookEvent from the constructor argument
                if (attr.ConstructorArguments.Length < 1)
                {
                    continue;
                }

                object? hookEventValue = attr.ConstructorArguments[0].Value;
                if (hookEventValue is not int hookEventInt)
                {
                    continue;
                }

                string? matcher = null;
                double timeout = 0;

                foreach (KeyValuePair<string, TypedConstant> namedArg in attr.NamedArguments)
                {
                    switch (namedArg.Key)
                    {
                        case "Matcher":
                            matcher = namedArg.Value.Value?.ToString();
                            break;
                        case "Timeout":
                            timeout = namedArg.Value.Value is double t ? t : 0;
                            break;
                    }
                }

                methods.Add(new HookMethodInfo
                {
                    MethodName = methodSymbol.Name,
                    HookEvent = hookEventInt,
                    Matcher = matcher,
                    Timeout = timeout
                });
            }
        }

        return methods;
    }

    private static string GenerateHookRegistrationExtension(INamedTypeSymbol classSymbol,
        List<HookMethodInfo> hookMethods)
    {
        string className = classSymbol.Name;
        string classNamespace = classSymbol.ContainingNamespace.ToDisplayString();
        string fullClassName = classSymbol.ToDisplayString();

        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Claude.AgentSdk.Protocol;");
        sb.AppendLine();
        sb.AppendLine($"namespace {classNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"///     Generated hook registration extensions for <see cref=\"{className}\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static class {className}HookRegistrationExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    ///     Gets the compiled hook dictionary from <see cref=\"{className}\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"instance\">The hooks instance.</param>");
        sb.AppendLine("    /// <returns>A dictionary of hooks keyed by HookEvent.</returns>");
        sb.AppendLine(
            $"    public static IReadOnlyDictionary<Claude.AgentSdk.Protocol.HookEvent, IReadOnlyList<HookMatcher>> GetHooksCompiled(this {fullClassName} instance)");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        var hooks = new Dictionary<Claude.AgentSdk.Protocol.HookEvent, IReadOnlyList<HookMatcher>>();");
        sb.AppendLine();

        // Group hooks by event type
        List<IGrouping<int, HookMethodInfo>> hooksByEvent = hookMethods.GroupBy(h => h.HookEvent).ToList();

        foreach (IGrouping<int, HookMethodInfo>? group in hooksByEvent)
        {
            string hookEventName = GetHookEventName(group.Key);

            sb.AppendLine($"        // {hookEventName} hooks");
            sb.AppendLine($"        hooks[Claude.AgentSdk.Protocol.HookEvent.{hookEventName}] = new List<HookMatcher>");
            sb.AppendLine("        {");

            // Group by matcher within this event
            List<IGrouping<string, HookMethodInfo>> matcherGroups = group.GroupBy(h => h.Matcher ?? "").ToList();

            foreach (IGrouping<string, HookMethodInfo>? matcherGroup in matcherGroups)
            {
                string? matcher = matcherGroup.Key;
                bool hasTimeout = matcherGroup.Any(m => m.Timeout > 0);
                double timeout = matcherGroup.Max(m => m.Timeout);

                sb.AppendLine("            new HookMatcher");
                sb.AppendLine("            {");

                if (!string.IsNullOrEmpty(matcher))
                {
                    sb.AppendLine($"                Matcher = \"{EscapeString(matcher)}\",");
                }

                if (hasTimeout)
                {
                    sb.AppendLine($"                Timeout = {timeout},");
                }

                sb.AppendLine("                Hooks = new HookCallback[]");
                sb.AppendLine("                {");

                foreach (HookMethodInfo? method in matcherGroup)
                {
                    sb.AppendLine($"                    instance.{method.MethodName},");
                }

                sb.AppendLine("                }");
                sb.AppendLine("            },");
            }

            sb.AppendLine("        };");
            sb.AppendLine();
        }

        sb.AppendLine("        return hooks;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GetHookEventName(int hookEvent)
    {
        return hookEvent switch
        {
            0 => "PreToolUse",
            1 => "PostToolUse",
            2 => "PostToolUseFailure",
            3 => "UserPromptSubmit",
            4 => "Stop",
            5 => "SubagentStart",
            6 => "SubagentStop",
            7 => "PreCompact",
            8 => "PermissionRequest",
            9 => "SessionStart",
            10 => "SessionEnd",
            11 => "Notification",
            _ => throw new InvalidOperationException($"Unknown HookEvent value: {hookEvent}")
        };
    }

    private static string EscapeString(string str)
    {
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    private sealed class HookMethodInfo
    {
        public required string MethodName { get; init; }
        public required int HookEvent { get; init; }
        public string? Matcher { get; init; }
        public double Timeout { get; init; }
    }
}
