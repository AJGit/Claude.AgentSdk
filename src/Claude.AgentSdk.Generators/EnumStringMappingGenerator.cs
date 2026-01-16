using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Claude.AgentSdk.Generators;

/// <summary>
///     Source generator that creates compile-time enum-to-string mapping methods for enums
///     marked with [GenerateEnumStrings].
/// </summary>
[Generator]
public sealed class EnumStringMappingGenerator : IIncrementalGenerator
{
    private const string GenerateEnumStringsAttribute = "Claude.AgentSdk.Attributes.GenerateEnumStringsAttribute";
    private const string EnumMemberAttribute = "System.Runtime.Serialization.EnumMemberAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all enums with [GenerateEnumStrings]
        var enumDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateEnum(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        // Combine with compilation
        var compilationAndEnums = context.CompilationProvider.Combine(enumDeclarations.Collect());

        // Generate source
        context.RegisterSourceOutput(compilationAndEnums, static (spc, source) => Execute(source.Left, source.Right!, spc));
    }

    private static bool IsCandidateEnum(SyntaxNode node)
    {
        return node is EnumDeclarationSyntax { AttributeLists.Count: > 0 };
    }

    private static EnumDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var enumDeclaration = (EnumDeclarationSyntax)context.Node;

        foreach (var attributeList in enumDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var symbol = context.SemanticModel.GetSymbolInfo(attribute).Symbol;
                if (symbol is IMethodSymbol methodSymbol)
                {
                    var attributeType = methodSymbol.ContainingType.ToDisplayString();
                    if (attributeType == GenerateEnumStringsAttribute)
                    {
                        return enumDeclaration;
                    }
                }
            }
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<EnumDeclarationSyntax?> enums, SourceProductionContext context)
    {
        if (enums.IsDefaultOrEmpty)
        {
            return;
        }

        var enumInfos = new List<EnumInfo>();

        foreach (var enumDeclaration in enums.Distinct())
        {
            if (enumDeclaration is null)
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(enumDeclaration.SyntaxTree);
            var enumSymbol = semanticModel.GetDeclaredSymbol(enumDeclaration);

            if (enumSymbol is null)
            {
                continue;
            }

            var enumInfo = GetEnumInfo(enumSymbol, compilation);
            if (enumInfo is not null)
            {
                enumInfos.Add(enumInfo);
            }
        }

        if (enumInfos.Count > 0)
        {
            var source = GenerateEnumStringMappings(enumInfos);
            context.AddSource("EnumStringMappings.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private static EnumInfo? GetEnumInfo(INamedTypeSymbol enumSymbol, Compilation compilation)
    {
        var generateAttrSymbol = compilation.GetTypeByMetadataName(GenerateEnumStringsAttribute);
        var enumMemberAttrSymbol = compilation.GetTypeByMetadataName(EnumMemberAttribute);

        if (generateAttrSymbol is null)
        {
            return null;
        }

        var generateAttr = enumSymbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, generateAttrSymbol));

        if (generateAttr is null)
        {
            return null;
        }

        // Get the default naming strategy from the attribute
        var namingStrategy = NamingStrategy.SnakeCase;
        foreach (var namedArg in generateAttr.NamedArguments)
        {
            if (namedArg.Key == "DefaultNaming" && namedArg.Value.Value is int strategyValue)
            {
                namingStrategy = (NamingStrategy)strategyValue;
            }
        }

        var members = new List<EnumMemberInfo>();

        foreach (var member in enumSymbol.GetMembers())
        {
            if (member is not IFieldSymbol { IsConst: true } fieldSymbol)
            {
                continue;
            }

            // Check for [EnumMember(Value = "...")] attribute
            string? explicitValue = null;
            if (enumMemberAttrSymbol is not null)
            {
                var enumMemberAttr = fieldSymbol.GetAttributes()
                    .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, enumMemberAttrSymbol));

                if (enumMemberAttr is not null)
                {
                    foreach (var namedArg in enumMemberAttr.NamedArguments)
                    {
                        if (namedArg.Key == "Value" && namedArg.Value.Value is string strValue)
                        {
                            explicitValue = strValue;
                            break;
                        }
                    }
                }
            }

            var stringValue = explicitValue ?? ApplyNamingStrategy(fieldSymbol.Name, namingStrategy);

            members.Add(new EnumMemberInfo
            {
                Name = fieldSymbol.Name,
                StringValue = stringValue
            });
        }

        return new EnumInfo
        {
            Name = enumSymbol.Name,
            Namespace = enumSymbol.ContainingNamespace.ToDisplayString(),
            FullName = enumSymbol.ToDisplayString(),
            Members = members
        };
    }

    private static string ApplyNamingStrategy(string name, NamingStrategy strategy)
    {
        return strategy switch
        {
            NamingStrategy.Exact => name,
            NamingStrategy.LowerCase => name.ToLowerInvariant(),
            NamingStrategy.SnakeCase => ToSnakeCase(name),
            NamingStrategy.KebabCase => ToKebabCase(name),
            _ => ToSnakeCase(name)
        };
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0)
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static string ToKebabCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0)
            {
                sb.Append('-');
            }
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static string GenerateEnumStringMappings(List<EnumInfo> enums)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine();

        // Collect all unique namespaces
        var namespaces = enums.Select(e => e.Namespace).Distinct().ToList();
        foreach (var ns in namespaces)
        {
            sb.AppendLine($"using {ns};");
        }

        sb.AppendLine();
        sb.AppendLine("namespace Claude.AgentSdk.Types;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("///     Generated enum string mapping methods.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static partial class EnumStringMappings");
        sb.AppendLine("{");

        foreach (var enumInfo in enums)
        {
            GenerateEnumMethods(sb, enumInfo);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateEnumMethods(StringBuilder sb, EnumInfo enumInfo)
    {
        var enumName = enumInfo.Name;
        var fullEnumName = enumInfo.FullName;

        // Generate ToJsonString extension method
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    ///     Converts a {enumName} enum to its JSON string representation.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static string ToJsonString(this {fullEnumName} value) => value switch");
        sb.AppendLine("    {");
        foreach (var member in enumInfo.Members)
        {
            sb.AppendLine($"        {fullEnumName}.{member.Name} => \"{EscapeString(member.StringValue)}\",");
        }
        sb.AppendLine($"        _ => throw new ArgumentOutOfRangeException(nameof(value), value, $\"Unknown {enumName} value: {{value}}\")");
        sb.AppendLine("    };");

        // Generate Parse method
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    ///     Parses a JSON string to a {enumName} enum value.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    /// <exception cref=\"ArgumentOutOfRangeException\">Thrown when the value is not a valid {enumName} string.</exception>");
        sb.AppendLine($"    public static {fullEnumName} Parse{enumName}(string value) => value switch");
        sb.AppendLine("    {");
        foreach (var member in enumInfo.Members)
        {
            sb.AppendLine($"        \"{EscapeString(member.StringValue)}\" => {fullEnumName}.{member.Name},");
        }
        sb.AppendLine($"        _ => throw new ArgumentOutOfRangeException(nameof(value), value, $\"Unknown {enumName} string: {{value}}\")");
        sb.AppendLine("    };");

        // Generate TryParse method
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    ///     Tries to parse a JSON string to a {enumName} enum value.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <returns>True if parsing succeeded, false otherwise.</returns>");
        sb.AppendLine($"    public static bool TryParse{enumName}(string? value, out {fullEnumName} result)");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (value)");
        sb.AppendLine("        {");
        foreach (var member in enumInfo.Members)
        {
            sb.AppendLine($"            case \"{EscapeString(member.StringValue)}\":");
            sb.AppendLine($"                result = {fullEnumName}.{member.Name};");
            sb.AppendLine("                return true;");
        }
        sb.AppendLine("            default:");
        sb.AppendLine("                result = default;");
        sb.AppendLine("                return false;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private static string EscapeString(string str)
    {
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    private enum NamingStrategy
    {
        Exact = 0,
        SnakeCase = 1,
        KebabCase = 2,
        LowerCase = 3
    }

    private sealed class EnumInfo
    {
        public required string Name { get; init; }
        public required string Namespace { get; init; }
        public required string FullName { get; init; }
        public required List<EnumMemberInfo> Members { get; init; }
    }

    private sealed class EnumMemberInfo
    {
        public required string Name { get; init; }
        public required string StringValue { get; init; }
    }
}
