using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Claude.AgentSdk.Generators;

/// <summary>
///     Source generator that creates compile-time agent registration code for classes
///     marked with [GenerateAgentRegistration].
/// </summary>
[Generator]
public sealed class AgentRegistrationGenerator : IIncrementalGenerator
{
    private const string GenerateAgentRegistrationAttribute =
        "Claude.AgentSdk.Attributes.GenerateAgentRegistrationAttribute";

    private const string ClaudeAgentAttribute = "Claude.AgentSdk.Attributes.ClaudeAgentAttribute";
    private const string AgentToolsAttribute = "Claude.AgentSdk.Attributes.AgentToolsAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all classes with [GenerateAgentRegistration]
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
                    if (attributeType == GenerateAgentRegistrationAttribute)
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

            List<AgentPropertyInfo> agentProperties = GetAgentProperties(classSymbol, compilation);
            if (agentProperties.Count > 0)
            {
                string source = GenerateAgentRegistrationExtension(classSymbol, agentProperties);
                context.AddSource($"{classSymbol.Name}AgentRegistrationExtensions.g.cs",
                    SourceText.From(source, Encoding.UTF8));
            }
        }
    }

    private static List<AgentPropertyInfo> GetAgentProperties(INamedTypeSymbol classSymbol, Compilation compilation)
    {
        List<AgentPropertyInfo> properties = [];
        INamedTypeSymbol? claudeAgentAttrSymbol = compilation.GetTypeByMetadataName(ClaudeAgentAttribute);
        INamedTypeSymbol? agentToolsAttrSymbol = compilation.GetTypeByMetadataName(AgentToolsAttribute);

        if (claudeAgentAttrSymbol is null)
        {
            return properties;
        }

        foreach (ISymbol? member in classSymbol.GetMembers())
        {
            if (member is not IPropertySymbol propertySymbol)
            {
                continue;
            }

            AttributeData? claudeAgentAttr = propertySymbol.GetAttributes()
                .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, claudeAgentAttrSymbol));

            if (claudeAgentAttr is null)
            {
                continue;
            }

            // Get the name from the constructor argument
            string name = claudeAgentAttr.ConstructorArguments[0].Value?.ToString() ?? propertySymbol.Name;

            string? description = null;
            string? model = null;

            foreach (KeyValuePair<string, TypedConstant> namedArg in claudeAgentAttr.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "Description":
                        description = namedArg.Value.Value?.ToString();
                        break;
                    case "Model":
                        model = namedArg.Value.Value?.ToString();
                        break;
                }
            }

            // Get tools from [AgentTools] attribute if present
            List<string> tools = [];
            if (agentToolsAttrSymbol is not null)
            {
                AttributeData? agentToolsAttr = propertySymbol.GetAttributes()
                    .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, agentToolsAttrSymbol));

                if (agentToolsAttr is not null && agentToolsAttr.ConstructorArguments.Length > 0)
                {
                    TypedConstant toolsArg = agentToolsAttr.ConstructorArguments[0];
                    if (toolsArg.Kind == TypedConstantKind.Array)
                    {
                        foreach (TypedConstant toolArg in toolsArg.Values)
                        {
                            if (toolArg.Value is string toolName)
                            {
                                tools.Add(toolName);
                            }
                        }
                    }
                }
            }

            properties.Add(new AgentPropertyInfo
            {
                PropertyName = propertySymbol.Name,
                AgentName = name,
                Description = description ?? $"Agent: {name}",
                Model = model,
                Tools = tools,
                IsStatic = propertySymbol.IsStatic
            });
        }

        return properties;
    }

    private static string GenerateAgentRegistrationExtension(INamedTypeSymbol classSymbol,
        List<AgentPropertyInfo> agentProperties)
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
        sb.AppendLine("using Claude.AgentSdk;");
        sb.AppendLine();
        sb.AppendLine($"namespace {classNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"///     Generated agent registration extensions for <see cref=\"{className}\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static class {className}AgentRegistrationExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    ///     Gets the compiled agent dictionary from <see cref=\"{className}\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    /// <param name=\"_\">The agents instance (unused, required for extension method syntax).</param>");
        sb.AppendLine("    /// <returns>A dictionary of agents keyed by name.</returns>");
        sb.AppendLine(
            $"    public static IReadOnlyDictionary<string, AgentDefinition> GetAgentsCompiled(this {fullClassName} _)");
        sb.AppendLine("    {");
        sb.AppendLine("        return new Dictionary<string, AgentDefinition>");
        sb.AppendLine("        {");

        foreach (AgentPropertyInfo? agent in agentProperties)
        {
            string promptExpression = agent.IsStatic
                ? $"{fullClassName}.{agent.PropertyName}"
                : $"_.{agent.PropertyName}";

            sb.AppendLine($"            [\"{EscapeString(agent.AgentName)}\"] = new AgentDefinition");
            sb.AppendLine("            {");
            sb.AppendLine($"                Description = \"{EscapeString(agent.Description)}\",");
            sb.AppendLine($"                Prompt = {promptExpression},");

            if (agent.Tools.Count > 0)
            {
                string toolsList = string.Join(", ", agent.Tools.Select(t => $"\"{EscapeString(t)}\""));
                sb.AppendLine($"                Tools = new[] {{ {toolsList} }},");
            }

            if (!string.IsNullOrEmpty(agent.Model))
            {
                sb.AppendLine($"                Model = \"{EscapeString(agent.Model!)}\"");
            }

            sb.AppendLine("            },");
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string EscapeString(string str)
    {
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    private sealed class AgentPropertyInfo
    {
        public required string PropertyName { get; init; }
        public required string AgentName { get; init; }
        public required string Description { get; init; }
        public string? Model { get; init; }
        public required List<string> Tools { get; init; }
        public bool IsStatic { get; init; }
    }
}
