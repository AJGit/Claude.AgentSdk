using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

namespace Claude.AgentSdk.Schema;

/// <summary>
///     Generates JSON schemas from C# types for structured outputs.
/// </summary>
public static class SchemaGenerator
{
    /// <summary>
    ///     Generates a JSON schema document suitable for Claude's structured outputs.
    /// </summary>
    /// <typeparam name="T">The type to generate a schema for.</typeparam>
    /// <param name="name">The name of the schema.</param>
    /// <param name="strict">Whether to use strict mode (additionalProperties: false).</param>
    /// <returns>A JsonElement containing the full structured output schema.</returns>
    public static JsonElement Generate<T>(string name, bool strict = true)
    {
        return Generate(typeof(T), name, strict);
    }

    /// <summary>
    ///     Generates a JSON schema document suitable for Claude's structured outputs.
    /// </summary>
    /// <param name="type">The type to generate a schema for.</param>
    /// <param name="name">The name of the schema.</param>
    /// <param name="strict">Whether to use strict mode (additionalProperties: false).</param>
    /// <returns>A JsonElement containing the full structured output schema.</returns>
    public static JsonElement Generate(Type type, string name, bool strict = true)
    {
        var schema = GenerateTypeSchema(type, strict, []);

        var wrapper = new JsonObject
        {
            ["type"] = "json_schema",
            ["json_schema"] = new JsonObject
            {
                ["name"] = name,
                ["strict"] = strict,
                ["schema"] = schema
            }
        };

        return JsonDocument.Parse(wrapper.ToJsonString()).RootElement;
    }

    /// <summary>
    ///     Generates just the inner schema portion (without the json_schema wrapper).
    /// </summary>
    /// <typeparam name="T">The type to generate a schema for.</typeparam>
    /// <param name="strict">Whether to use strict mode.</param>
    /// <returns>A JsonNode containing the schema.</returns>
    public static JsonNode GenerateInnerSchema<T>(bool strict = true)
    {
        return GenerateTypeSchema(typeof(T), strict, []);
    }

    private static JsonNode GenerateTypeSchema(Type type, bool strict, HashSet<Type> visited)
    {
        // Try each type handler in order
        if (TryHandleNullable(type, strict, visited, out var nullableSchema))
        {
            return nullableSchema!;
        }

        if (TryHandlePrimitive(type, out var primitiveSchema))
        {
            return primitiveSchema!;
        }

        if (TryHandleEnum(type, out var enumSchema))
        {
            return enumSchema!;
        }

        if (TryHandleArray(type, strict, visited, out var arraySchema))
        {
            return arraySchema!;
        }

        if (TryHandleList(type, strict, visited, out var listSchema))
        {
            return listSchema!;
        }

        if (TryHandleDictionary(type, strict, visited, out var dictSchema))
        {
            return dictSchema!;
        }

        if (TryHandleComplexObject(type, strict, visited, out var objectSchema))
        {
            return objectSchema!;
        }

        // Fallback
        return new JsonObject { ["type"] = "object" };
    }

    private static bool TryHandleNullable(Type type, bool strict, HashSet<Type> visited, out JsonNode? schema)
    {
        schema = null;
        var underlyingNullable = Nullable.GetUnderlyingType(type);
        if (underlyingNullable is null)
        {
            return false;
        }

        var innerSchema = GenerateTypeSchema(underlyingNullable, strict, visited);
        schema = MakeNullable(innerSchema);
        return true;
    }

    /// <remarks>
    ///     Complexity is due to mapping multiple primitive types - each is a simple type check with no nested logic.
    /// </remarks>
    private static bool TryHandlePrimitive(Type type, out JsonNode? schema)
    {
        schema = type switch
        {
            _ when type == typeof(string) => new JsonObject { ["type"] = "string" },
            _ when type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)
                => new JsonObject { ["type"] = "integer" },
            _ when type == typeof(double) || type == typeof(float) || type == typeof(decimal)
                => new JsonObject { ["type"] = "number" },
            _ when type == typeof(bool) => new JsonObject { ["type"] = "boolean" },
            _ => null
        };
        return schema is not null;
    }

    private static bool TryHandleEnum(Type type, out JsonNode? schema)
    {
        schema = null;
        if (!type.IsEnum)
        {
            return false;
        }

        var enumValues = new JsonArray();
        foreach (var enumName in Enum.GetNames(type))
        {
            enumValues.Add(ToSnakeCase(enumName));
        }

        schema = new JsonObject
        {
            ["type"] = "string",
            ["enum"] = enumValues
        };
        return true;
    }

    private static bool TryHandleArray(Type type, bool strict, HashSet<Type> visited, out JsonNode? schema)
    {
        schema = null;
        if (!type.IsArray)
        {
            return false;
        }

        var elementType = type.GetElementType()!;
        schema = new JsonObject
        {
            ["type"] = "array",
            ["items"] = GenerateTypeSchema(elementType, strict, visited)
        };
        return true;
    }

    private static bool TryHandleList(Type type, bool strict, HashSet<Type> visited, out JsonNode? schema)
    {
        schema = null;
        if (!IsGenericList(type, out var listElementType))
        {
            return false;
        }

        schema = new JsonObject
        {
            ["type"] = "array",
            ["items"] = GenerateTypeSchema(listElementType, strict, visited)
        };
        return true;
    }

    private static bool TryHandleDictionary(Type type, bool strict, HashSet<Type> visited, out JsonNode? schema)
    {
        schema = null;
        if (!IsGenericDictionary(type, out var keyType, out var valueType))
        {
            return false;
        }

        if (keyType != typeof(string))
        {
            throw new ArgumentException($"Dictionary keys must be strings for JSON schema, got {keyType.Name}");
        }

        schema = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = GenerateTypeSchema(valueType, strict, visited)
        };
        return true;
    }

    private static bool TryHandleComplexObject(Type type, bool strict, HashSet<Type> visited, out JsonNode? schema)
    {
        schema = null;
        if (type is { IsClass: false, IsValueType: false })
        {
            return false;
        }

        // Check for circular reference
        if (!visited.Add(type))
        {
            schema = new JsonObject { ["type"] = "object" };
            return true;
        }

        schema = BuildObjectSchema(type, strict, visited);
        visited.Remove(type);
        return true;
    }

    private static JsonObject BuildObjectSchema(Type type, bool strict, HashSet<Type> visited)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        var typeProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

        foreach (var prop in typeProps)
        {
            var propName = ToSnakeCase(prop.Name);
            var propSchema = GenerateTypeSchema(prop.PropertyType, strict, visited);

            AddPropertyDescription(prop, propSchema);
            properties[propName] = propSchema;

            if (!IsNullableProperty(prop))
            {
                required.Add(propName);
            }
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        if (strict)
        {
            schema["additionalProperties"] = false;
        }

        AddTypeDescription(type, schema);

        return schema;
    }

    private static void AddPropertyDescription(PropertyInfo prop, JsonNode propSchema)
    {
        var descAttr = prop.GetCustomAttribute<DescriptionAttribute>();
        var schemaDescAttr = prop.GetCustomAttribute<SchemaDescriptionAttribute>();
        var description = schemaDescAttr?.Description ?? descAttr?.Description;

        if (description is not null && propSchema is JsonObject propObj)
        {
            propObj["description"] = description;
        }
    }

    private static void AddTypeDescription(Type type, JsonObject schema)
    {
        var typeDescAttr = type.GetCustomAttribute<DescriptionAttribute>();
        var typeSchemaDescAttr = type.GetCustomAttribute<SchemaDescriptionAttribute>();
        var typeDescription = typeSchemaDescAttr?.Description ?? typeDescAttr?.Description;

        if (typeDescription is not null)
        {
            schema["description"] = typeDescription;
        }
    }

    private static JsonNode MakeNullable(JsonNode schema)
    {
        if (schema is JsonObject obj && obj.TryGetPropertyValue("type", out var typeNode))
        {
            var typeValue = typeNode?.GetValue<string>();
            if (typeValue is not null)
            {
                obj["type"] = new JsonArray { typeValue, "null" };
            }
        }

        return schema;
    }

    private static bool IsNullableProperty(PropertyInfo prop)
    {
        if (Nullable.GetUnderlyingType(prop.PropertyType) is not null)
        {
            return true;
        }

        var nullabilityContext = new NullabilityInfoContext();
        var nullabilityInfo = nullabilityContext.Create(prop);

        return nullabilityInfo.WriteState == NullabilityState.Nullable ||
               nullabilityInfo.ReadState == NullabilityState.Nullable;
    }

    private static bool IsGenericList(Type type, out Type elementType)
    {
        elementType = typeof(object);

        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            if (IsListGenericType(genericDef))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
        }

        // Check if it implements IEnumerable<T>
        var enumerableInterface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableInterface is not null && type != typeof(string))
        {
            elementType = enumerableInterface.GetGenericArguments()[0];
            return true;
        }

        return false;
    }

    private static bool IsListGenericType(Type genericDef)
    {
        return genericDef == typeof(List<>) ||
               genericDef == typeof(IList<>) ||
               genericDef == typeof(IReadOnlyList<>) ||
               genericDef == typeof(IEnumerable<>) ||
               genericDef == typeof(ICollection<>) ||
               genericDef == typeof(IReadOnlyCollection<>);
    }

    private static bool IsGenericDictionary(Type type, out Type keyType, out Type valueType)
    {
        keyType = typeof(object);
        valueType = typeof(object);

        if (!type.IsGenericType)
        {
            return false;
        }

        var genericDef = type.GetGenericTypeDefinition();
        if (genericDef != typeof(Dictionary<,>) &&
            genericDef != typeof(IDictionary<,>) &&
            genericDef != typeof(IReadOnlyDictionary<,>))
        {
            return false;
        }

        var args = type.GetGenericArguments();
        keyType = args[0];
        valueType = args[1];
        return true;
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var result = new StringBuilder();
        result.Append(char.ToLowerInvariant(name[0]));

        for (var i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}

/// <summary>
///     Attribute to provide a description for a property or type in the generated schema.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct)]
internal sealed class SchemaDescriptionAttribute(string description) : Attribute
{
    public string Description { get; } = description;
}

/// <summary>
///     Attribute to specify enum values for a string property.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class SchemaEnumAttribute(params string[] values) : Attribute
{
    public string[] Values { get; } = values;
}
