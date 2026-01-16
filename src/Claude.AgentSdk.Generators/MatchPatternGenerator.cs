using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Claude.AgentSdk.Generators;

/// <summary>
///     Source generator that creates functional Match extension methods for types
///     marked with [GenerateMatch] that use JsonDerivedType for polymorphism.
/// </summary>
[Generator]
public sealed class MatchPatternGenerator : IIncrementalGenerator
{
    private const string GenerateMatchAttribute = "Claude.AgentSdk.Attributes.GenerateMatchAttribute";
    private const string JsonDerivedTypeAttribute = "System.Text.Json.Serialization.JsonDerivedTypeAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all classes with [GenerateMatch]
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateClass(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        // Combine with compilation
        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        // Generate source
        context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Left, source.Right!, spc));
    }

    private static bool IsCandidateClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { AttributeLists.Count: > 0 } or
            RecordDeclarationSyntax { AttributeLists.Count: > 0 };
    }

    private static TypeDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;

        foreach (var attributeList in typeDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var symbol = context.SemanticModel.GetSymbolInfo(attribute).Symbol;
                if (symbol is IMethodSymbol methodSymbol)
                {
                    var attributeType = methodSymbol.ContainingType.ToDisplayString();
                    if (attributeType == GenerateMatchAttribute)
                    {
                        return typeDeclaration;
                    }
                }
            }
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<TypeDeclarationSyntax?> types, SourceProductionContext context)
    {
        if (types.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var typeDeclaration in types.Distinct())
        {
            if (typeDeclaration is null)
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration);

            if (typeSymbol is null)
            {
                continue;
            }

            var matchInfo = GetMatchInfo(typeSymbol, compilation);
            if (matchInfo is not null && matchInfo.DerivedTypes.Count > 0)
            {
                var source = GenerateMatchExtension(matchInfo);
                context.AddSource($"{matchInfo.TypeName}MatchExtensions.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }
    }

    private static MatchInfo? GetMatchInfo(INamedTypeSymbol typeSymbol, Compilation compilation)
    {
        var jsonDerivedTypeAttrSymbol = compilation.GetTypeByMetadataName(JsonDerivedTypeAttribute);
        if (jsonDerivedTypeAttrSymbol is null)
        {
            return null;
        }

        var derivedTypes = new List<DerivedTypeInfo>();

        // Find all [JsonDerivedType] attributes on the base type
        foreach (var attr in typeSymbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, jsonDerivedTypeAttrSymbol))
            {
                continue;
            }

            if (attr.ConstructorArguments.Length < 1)
            {
                continue;
            }

            var derivedTypeArg = attr.ConstructorArguments[0];
            if (derivedTypeArg.Value is not INamedTypeSymbol derivedTypeSymbol)
            {
                continue;
            }

            // Get the discriminator value (second argument if present)
            string? discriminator = null;
            if (attr.ConstructorArguments.Length >= 2)
            {
                discriminator = attr.ConstructorArguments[1].Value?.ToString();
            }

            // Create a parameter name from the derived type name
            var paramName = ToCamelCase(derivedTypeSymbol.Name);

            derivedTypes.Add(new DerivedTypeInfo
            {
                TypeSymbol = derivedTypeSymbol,
                FullTypeName = derivedTypeSymbol.ToDisplayString(),
                TypeName = derivedTypeSymbol.Name,
                ParameterName = paramName,
                Discriminator = discriminator
            });
        }

        return new MatchInfo
        {
            TypeSymbol = typeSymbol,
            FullTypeName = typeSymbol.ToDisplayString(),
            TypeName = typeSymbol.Name,
            Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
            DerivedTypes = derivedTypes
        };
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        // Handle acronyms and consecutive uppercase letters
        var result = new StringBuilder();
        var i = 0;

        // Find the first lowercase letter or the end
        while (i < name.Length && char.IsUpper(name[i]))
        {
            if (i == 0)
            {
                result.Append(char.ToLowerInvariant(name[i]));
            }
            else if (i + 1 < name.Length && char.IsLower(name[i + 1]))
            {
                // We're at the last uppercase letter before a lowercase
                result.Append(char.ToLowerInvariant(name[i]));
            }
            else if (i + 1 >= name.Length)
            {
                // We're at the end
                result.Append(char.ToLowerInvariant(name[i]));
            }
            else
            {
                result.Append(char.ToLowerInvariant(name[i]));
            }
            i++;
        }

        // Append the rest
        while (i < name.Length)
        {
            result.Append(name[i]);
            i++;
        }

        return result.ToString();
    }

    private static string GenerateMatchExtension(MatchInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"namespace {info.Namespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"///     Generated Match extension methods for <see cref=\"{info.TypeName}\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static class {info.TypeName}MatchExtensions");
        sb.AppendLine("{");

        // Generate Match<TResult> with all required parameters
        GenerateMatchMethod(sb, info, withDefault: false);

        // Generate Match<TResult> with defaultCase for partial matching
        GenerateMatchMethod(sb, info, withDefault: true);

        // Generate void Match (action-based)
        GenerateVoidMatchMethod(sb, info, withDefault: false);

        // Generate void Match with defaultCase
        GenerateVoidMatchMethod(sb, info, withDefault: true);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateMatchMethod(StringBuilder sb, MatchInfo info, bool withDefault)
    {
        var paramList = new List<string>();
        foreach (var derived in info.DerivedTypes)
        {
            var optionalMark = withDefault ? "?" : "";
            paramList.Add($"Func<{derived.FullTypeName}, TResult>{optionalMark} {derived.ParameterName} = null!");
        }

        if (withDefault)
        {
            paramList.Add("Func<TResult>? defaultCase = null");
        }

        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    ///     Matches the {info.TypeName} against its derived types and returns a result.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <typeparam name=\"TResult\">The type of the result.</typeparam>");
        sb.AppendLine($"    public static TResult Match<TResult>(");
        sb.AppendLine($"        this {info.FullTypeName} value,");
        sb.AppendLine($"        {string.Join(",\n        ", paramList)})");
        sb.AppendLine("    {");
        sb.AppendLine("        return value switch");
        sb.AppendLine("        {");

        foreach (var derived in info.DerivedTypes)
        {
            if (withDefault)
            {
                sb.AppendLine($"            {derived.FullTypeName} x when {derived.ParameterName} is not null => {derived.ParameterName}(x),");
            }
            else
            {
                sb.AppendLine($"            {derived.FullTypeName} x => {derived.ParameterName}(x),");
            }
        }

        if (withDefault)
        {
            sb.AppendLine($"            _ when defaultCase is not null => defaultCase(),");
            sb.AppendLine($"            _ => throw new InvalidOperationException($\"Unhandled {info.TypeName} type: {{value.GetType().Name}}\")");
        }
        else
        {
            sb.AppendLine($"            _ => throw new InvalidOperationException($\"Unhandled {info.TypeName} type: {{value.GetType().Name}}\")");
        }

        sb.AppendLine("        };");
        sb.AppendLine("    }");
    }

    private static void GenerateVoidMatchMethod(StringBuilder sb, MatchInfo info, bool withDefault)
    {
        var paramList = new List<string>();
        foreach (var derived in info.DerivedTypes)
        {
            var optionalMark = withDefault ? "?" : "";
            paramList.Add($"Action<{derived.FullTypeName}>{optionalMark} {derived.ParameterName} = null!");
        }

        if (withDefault)
        {
            paramList.Add("Action? defaultCase = null");
        }

        var methodName = withDefault ? "MatchAction" : "Match";

        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    ///     Matches the {info.TypeName} against its derived types and executes an action.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static void {methodName}(");
        sb.AppendLine($"        this {info.FullTypeName} value,");
        sb.AppendLine($"        {string.Join(",\n        ", paramList)})");
        sb.AppendLine("    {");
        sb.AppendLine("        switch (value)");
        sb.AppendLine("        {");

        foreach (var derived in info.DerivedTypes)
        {
            if (withDefault)
            {
                sb.AppendLine($"            case {derived.FullTypeName} x when {derived.ParameterName} is not null:");
                sb.AppendLine($"                {derived.ParameterName}(x);");
                sb.AppendLine("                return;");
            }
            else
            {
                sb.AppendLine($"            case {derived.FullTypeName} x:");
                sb.AppendLine($"                {derived.ParameterName}(x);");
                sb.AppendLine("                return;");
            }
        }

        if (withDefault)
        {
            sb.AppendLine("            default:");
            sb.AppendLine("                if (defaultCase is not null) defaultCase();");
            sb.AppendLine($"                else throw new InvalidOperationException($\"Unhandled {info.TypeName} type: {{value.GetType().Name}}\");");
            sb.AppendLine("                return;");
        }
        else
        {
            sb.AppendLine("            default:");
            sb.AppendLine($"                throw new InvalidOperationException($\"Unhandled {info.TypeName} type: {{value.GetType().Name}}\");");
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
    }

    private sealed class MatchInfo
    {
        public required INamedTypeSymbol TypeSymbol { get; init; }
        public required string FullTypeName { get; init; }
        public required string TypeName { get; init; }
        public required string Namespace { get; init; }
        public required List<DerivedTypeInfo> DerivedTypes { get; init; }
    }

    private sealed class DerivedTypeInfo
    {
        public required INamedTypeSymbol TypeSymbol { get; init; }
        public required string FullTypeName { get; init; }
        public required string TypeName { get; init; }
        public required string ParameterName { get; init; }
        public string? Discriminator { get; init; }
    }
}
