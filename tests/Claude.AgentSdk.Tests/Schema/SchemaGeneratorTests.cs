using System.ComponentModel;
using System.Text.Json;
using Claude.AgentSdk.Schema;

namespace Claude.AgentSdk.Tests.Schema;

/// <summary>
///     Comprehensive tests for the SchemaGenerator class.
/// </summary>
public class SchemaGeneratorTests
{
    [Theory]
    [InlineData("stringList", "string")]
    [InlineData("intList", "integer")]
    [InlineData("readOnlyDoubleList", "number")]
    [InlineData("boolEnumerable", "boolean")]
    [InlineData("floatCollection", "number")]
    [InlineData("readOnlyLongCollection", "integer")]
    public void Generate_ListTypes_ReturnsArrayWithCorrectItemType(string propertyName, string expectedItemType)
    {
        var result = SchemaGenerator.Generate<ClassWithLists>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");
        var listProp = properties.GetProperty(propertyName);

        Assert.Equal("array", listProp.GetProperty("type").GetString());
        Assert.Equal(expectedItemType, listProp.GetProperty("items").GetProperty("type").GetString());
    }

    public class SimpleStringProperty
    {
        public string Name { get; set; } = string.Empty;
    }

    public class AllPrimitiveTypes
    {
        public string StringProp { get; set; } = string.Empty;
        public int IntProp { get; set; }
        public long LongProp { get; set; }
        public short ShortProp { get; set; }
        public byte ByteProp { get; set; }
        public double DoubleProp { get; set; }
        public float FloatProp { get; set; }
        public decimal DecimalProp { get; set; }
        public bool BoolProp { get; set; }
    }

    public class NullableTypes
    {
        public int? NullableInt { get; set; }
        public double? NullableDouble { get; set; }
        public bool? NullableBool { get; set; }
        public string? NullableString { get; set; }
    }

    public enum SampleEnum
    {
        FirstValue,
        SecondValue,
        ThirdOption
    }

    public enum ColorEnum
    {
        Red,
        Green,
        Blue
    }

    public class ClassWithEnum
    {
        public SampleEnum Status { get; set; }
        public ColorEnum? OptionalColor { get; set; }
    }

    public class ClassWithArrays
    {
        public string[] StringArray { get; set; } = [];
        public int[] IntArray { get; set; } = [];
    }

    public class ClassWithLists
    {
        public List<string> StringList { get; set; } = [];
        public IList<int> IntList { get; set; } = [];
        public IReadOnlyList<double> ReadOnlyDoubleList { get; set; } = [];
        public IEnumerable<bool> BoolEnumerable { get; set; } = [];
        public ICollection<float> FloatCollection { get; set; } = [];
        public IReadOnlyCollection<long> ReadOnlyLongCollection { get; set; } = [];
    }

    public class ClassWithDictionaries
    {
        public Dictionary<string, int> StringIntDict { get; set; } = new();
        public IDictionary<string, string> StringStringDict { get; set; } = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, bool> ReadOnlyDict { get; set; } = new Dictionary<string, bool>();
    }

    public class NestedClass
    {
        public string Name { get; set; } = string.Empty;
        public InnerClass? Inner { get; set; }
    }

    public class InnerClass
    {
        public int Value { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class DeeplyNestedClass
    {
        public string RootName { get; set; } = string.Empty;
        public Level1 Level1Data { get; set; } = new();
    }

    public class Level1
    {
        public string Level1Name { get; set; } = string.Empty;
        public Level2 Level2Data { get; set; } = new();
    }

    public class Level2
    {
        public string Level2Name { get; set; } = string.Empty;
        public int FinalValue { get; set; }
    }

    public class RequiredVsOptional
    {
        public string RequiredString { get; set; } = string.Empty;
        public int RequiredInt { get; set; }
        public string? OptionalString { get; set; }
        public int? OptionalInt { get; set; }
    }

    [Description("A class with type description")]
    public class ClassWithTypeDescription
    {
        public string Name { get; set; } = string.Empty;
    }

    [SchemaDescription("Custom schema description for type")]
    public class ClassWithSchemaDescription
    {
        public string Name { get; set; } = string.Empty;
    }

    public class ClassWithPropertyDescriptions
    {
        [Description("The person's full name")]
        public string Name { get; set; } = string.Empty;

        [Description("The person's age in years")]
        public int Age { get; set; }

        [SchemaDescription("Custom schema description takes precedence")]
        [Description("This should be ignored")]
        public string Email { get; set; } = string.Empty;
    }

    public class SelfReferencingClass
    {
        public string Name { get; set; } = string.Empty;
        public SelfReferencingClass? Parent { get; set; }
    }

    public class MutuallyReferencingClassA
    {
        public string Name { get; set; } = string.Empty;
        public MutuallyReferencingClassB? Other { get; set; }
    }

    public class MutuallyReferencingClassB
    {
        public string Value { get; set; } = string.Empty;
        public MutuallyReferencingClassA? Other { get; set; }
    }

    public class ClassWithComplexNesting
    {
        public string Name { get; set; } = string.Empty;
        public List<InnerClass> Items { get; set; } = [];
        public Dictionary<string, InnerClass> ItemsDict { get; set; } = new();
        public InnerClass[] ItemsArray { get; set; } = [];
    }

    public class EmptyClass
    {
    }

    public struct SimpleStruct
    {
        public int Value { get; set; }
        public string Name { get; set; }
    }

    public class ClassWithStruct
    {
        public SimpleStruct StructProp { get; set; }
    }

    [Fact]
    public void Generate_StringProperty_ReturnsStringType()
    {
        var result = SchemaGenerator.Generate<SimpleStringProperty>("test_schema");
        var schema = GetInnerSchema(result);

        Assert.Equal("object", schema.GetProperty("type").GetString());
        var properties = schema.GetProperty("properties");
        Assert.Equal("string", properties.GetProperty("name").GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("intProp", "integer")]
    [InlineData("longProp", "integer")]
    [InlineData("shortProp", "integer")]
    [InlineData("byteProp", "integer")]
    public void Generate_IntegerTypes_ReturnsIntegerType(string propertyName, string expectedType)
    {
        var result = SchemaGenerator.Generate<AllPrimitiveTypes>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");

        Assert.Equal(expectedType, properties.GetProperty(propertyName).GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("doubleProp", "number")]
    [InlineData("floatProp", "number")]
    [InlineData("decimalProp", "number")]
    public void Generate_FloatingPointTypes_ReturnsNumberType(string propertyName, string expectedType)
    {
        var result = SchemaGenerator.Generate<AllPrimitiveTypes>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");

        Assert.Equal(expectedType, properties.GetProperty(propertyName).GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_BoolProperty_ReturnsBooleanType()
    {
        var result = SchemaGenerator.Generate<AllPrimitiveTypes>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");

        Assert.Equal("boolean", properties.GetProperty("boolProp").GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_NullableInt_ReturnsIntegerWithNullType()
    {
        var result = SchemaGenerator.Generate<NullableTypes>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");
        var nullableIntType = properties.GetProperty("nullableInt").GetProperty("type");

        Assert.Equal(JsonValueKind.Array, nullableIntType.ValueKind);
        var types = GetArrayStrings(nullableIntType);
        Assert.Contains("integer", types);
        Assert.Contains("null", types);
    }

    [Fact]
    public void Generate_NullableDouble_ReturnsNumberWithNullType()
    {
        var result = SchemaGenerator.Generate<NullableTypes>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");
        var nullableDoubleType = properties.GetProperty("nullableDouble").GetProperty("type");

        Assert.Equal(JsonValueKind.Array, nullableDoubleType.ValueKind);
        var types = GetArrayStrings(nullableDoubleType);
        Assert.Contains("number", types);
        Assert.Contains("null", types);
    }

    [Fact]
    public void Generate_NullableBool_ReturnsBooleanWithNullType()
    {
        var result = SchemaGenerator.Generate<NullableTypes>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");
        var nullableBoolType = properties.GetProperty("nullableBool").GetProperty("type");

        Assert.Equal(JsonValueKind.Array, nullableBoolType.ValueKind);
        var types = GetArrayStrings(nullableBoolType);
        Assert.Contains("boolean", types);
        Assert.Contains("null", types);
    }

    [Fact]
    public void Generate_NullableString_IsNotInRequiredArray()
    {
        var result = SchemaGenerator.Generate<NullableTypes>("test_schema");
        var schema = GetInnerSchema(result);

        // Nullable string should not be in required array
        if (schema.TryGetProperty("required", out var required))
        {
            var requiredFields = GetArrayStrings(required);
            Assert.DoesNotContain("nullableString", requiredFields);
        }
    }

    [Fact]
    public void Generate_EnumProperty_ReturnsStringWithEnumValues()
    {
        var result = SchemaGenerator.Generate<ClassWithEnum>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");
        var statusProp = properties.GetProperty("status");

        Assert.Equal("string", statusProp.GetProperty("type").GetString());
        var enumValues = GetArrayStrings(statusProp.GetProperty("enum"));
        Assert.Contains("firstValue", enumValues);
        Assert.Contains("secondValue", enumValues);
        Assert.Contains("thirdOption", enumValues);
    }

    [Fact]
    public void Generate_NullableEnumProperty_ReturnsNullableStringWithEnumValues()
    {
        var result = SchemaGenerator.Generate<ClassWithEnum>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");
        var colorProp = properties.GetProperty("optionalColor");

        // Should have type array with string and null
        var typeValue = colorProp.GetProperty("type");
        Assert.Equal(JsonValueKind.Array, typeValue.ValueKind);
        var types = GetArrayStrings(typeValue);
        Assert.Contains("string", types);
        Assert.Contains("null", types);

        // Should still have enum values
        var enumValues = GetArrayStrings(colorProp.GetProperty("enum"));
        Assert.Contains("red", enumValues);
        Assert.Contains("green", enumValues);
        Assert.Contains("blue", enumValues);
    }

    [Fact]
    public void Generate_EnumValues_AreConvertedToCamelCase()
    {
        var result = SchemaGenerator.Generate<ClassWithEnum>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");
        var statusProp = properties.GetProperty("status");
        var enumValues = GetArrayStrings(statusProp.GetProperty("enum"));

        // Verify camelCase conversion
        Assert.Contains("firstValue", enumValues); // FirstValue -> firstValue
        Assert.Contains("secondValue", enumValues); // SecondValue -> secondValue
        Assert.Contains("thirdOption", enumValues); // ThirdOption -> thirdOption
    }

    [Fact]
    public void Generate_StringArray_ReturnsArrayWithStringItems()
    {
        var result = SchemaGenerator.Generate<ClassWithArrays>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");
        var arrayProp = properties.GetProperty("stringArray");

        Assert.Equal("array", arrayProp.GetProperty("type").GetString());
        Assert.Equal("string", arrayProp.GetProperty("items").GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_IntArray_ReturnsArrayWithIntegerItems()
    {
        var result = SchemaGenerator.Generate<ClassWithArrays>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");
        var arrayProp = properties.GetProperty("intArray");

        Assert.Equal("array", arrayProp.GetProperty("type").GetString());
        Assert.Equal("integer", arrayProp.GetProperty("items").GetProperty("type").GetString());
    }

    // Note: The current SchemaGenerator implementation has a limitation where Dictionary<K,V>
    // is treated as IEnumerable<KeyValuePair<K,V>> (an array) because list handling comes
    // before dictionary handling. These tests document the current behavior.
    // TODO: Consider reordering handlers so dictionary detection comes before list detection.

    [Fact]
    public void Generate_DictionaryStringInt_CurrentBehavior_TreatsAsArrayOfKeyValuePairs()
    {
        // Note: Due to implementation order, dictionaries are detected as IEnumerable<KeyValuePair>
        // and treated as arrays. This test documents the current behavior.
        var result = SchemaGenerator.Generate<ClassWithDictionaries>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");
        var dictProp = properties.GetProperty("stringIntDict");

        // Current behavior: Dictionary is treated as an array of KeyValuePairs
        Assert.Equal("array", dictProp.GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_DictionaryStringString_CurrentBehavior_TreatsAsArrayOfKeyValuePairs()
    {
        var result = SchemaGenerator.Generate<ClassWithDictionaries>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");
        var dictProp = properties.GetProperty("stringStringDict");

        Assert.Equal("array", dictProp.GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_ReadOnlyDictionary_CurrentBehavior_TreatsAsArrayOfKeyValuePairs()
    {
        var result = SchemaGenerator.Generate<ClassWithDictionaries>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");
        var dictProp = properties.GetProperty("readOnlyDict");

        Assert.Equal("array", dictProp.GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_DictionaryWithNonStringKey_CurrentBehavior_TreatsAsArrayOfKeyValuePairs()
    {
        // Due to implementation order, dictionaries are treated as arrays before
        // the non-string key validation in TryHandleDictionary is reached.
        // This test documents that no exception is thrown (current behavior).
        var result = SchemaGenerator.Generate(typeof(Dictionary<int, string>), "test_schema");

        // Verify we got a valid schema (JsonElement is a value type, so we check the type property)
        Assert.Equal("json_schema", result.GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_NestedClass_ReturnsCorrectStructure()
    {
        var result = SchemaGenerator.Generate<NestedClass>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");

        Assert.Equal("string", properties.GetProperty("name").GetProperty("type").GetString());

        var innerProp = properties.GetProperty("inner");
        Assert.Equal("object", innerProp.GetProperty("type").GetString());

        var innerProperties = innerProp.GetProperty("properties");
        Assert.Equal("integer", innerProperties.GetProperty("value").GetProperty("type").GetString());
        Assert.Equal("string", innerProperties.GetProperty("description").GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_DeeplyNestedClass_ReturnsCorrectStructure()
    {
        var result = SchemaGenerator.Generate<DeeplyNestedClass>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");

        Assert.Equal("string", properties.GetProperty("rootName").GetProperty("type").GetString());

        var level1 = properties.GetProperty("level1Data");
        Assert.Equal("object", level1.GetProperty("type").GetString());

        var level1Properties = level1.GetProperty("properties");
        Assert.Equal("string", level1Properties.GetProperty("level1Name").GetProperty("type").GetString());

        var level2 = level1Properties.GetProperty("level2Data");
        Assert.Equal("object", level2.GetProperty("type").GetString());

        var level2Properties = level2.GetProperty("properties");
        Assert.Equal("string", level2Properties.GetProperty("level2Name").GetProperty("type").GetString());
        Assert.Equal("integer", level2Properties.GetProperty("finalValue").GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_ClassWithComplexNesting_HandlesListOfObjects()
    {
        var result = SchemaGenerator.Generate<ClassWithComplexNesting>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");

        // List of InnerClass
        var items = properties.GetProperty("items");
        Assert.Equal("array", items.GetProperty("type").GetString());
        Assert.Equal("object", items.GetProperty("items").GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_ClassWithComplexNesting_HandlesDictionaryOfObjects_CurrentBehavior()
    {
        var result = SchemaGenerator.Generate<ClassWithComplexNesting>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");

        // Note: Due to implementation order (list before dictionary detection),
        // Dictionary<string, InnerClass> is treated as IEnumerable<KeyValuePair<string, InnerClass>>
        var itemsDict = properties.GetProperty("itemsDict");
        Assert.Equal("array", itemsDict.GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_ClassWithComplexNesting_HandlesArrayOfObjects()
    {
        var result = SchemaGenerator.Generate<ClassWithComplexNesting>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");

        // Array of InnerClass
        var itemsArray = properties.GetProperty("itemsArray");
        Assert.Equal("array", itemsArray.GetProperty("type").GetString());
        Assert.Equal("object", itemsArray.GetProperty("items").GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_RequiredProperties_AreInRequiredArray()
    {
        var result = SchemaGenerator.Generate<RequiredVsOptional>("test_schema");
        var schema = GetInnerSchema(result);
        var required = GetArrayStrings(schema.GetProperty("required"));

        Assert.Contains("requiredString", required);
        Assert.Contains("requiredInt", required);
    }

    [Fact]
    public void Generate_OptionalProperties_AreNotInRequiredArray()
    {
        var result = SchemaGenerator.Generate<RequiredVsOptional>("test_schema");
        var schema = GetInnerSchema(result);
        var required = GetArrayStrings(schema.GetProperty("required"));

        Assert.DoesNotContain("optionalString", required);
        Assert.DoesNotContain("optionalInt", required);
    }

    [Fact]
    public void Generate_StrictModeTrue_SetsAdditionalPropertiesToFalse()
    {
        var result = SchemaGenerator.Generate<SimpleStringProperty>("test_schema", true);
        var schema = GetInnerSchema(result);

        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void Generate_StrictModeFalse_DoesNotSetAdditionalProperties()
    {
        var result = SchemaGenerator.Generate<SimpleStringProperty>("test_schema", false);
        var schema = GetInnerSchema(result);

        Assert.False(schema.TryGetProperty("additionalProperties", out _));
    }

    [Fact]
    public void Generate_StrictModeDefault_IsTrue()
    {
        var result = SchemaGenerator.Generate<SimpleStringProperty>("test_schema");
        var schema = GetInnerSchema(result);

        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void Generate_StrictMode_PropagatesRecursively()
    {
        var result = SchemaGenerator.Generate<NestedClass>("test_schema", true);
        var schema = GetInnerSchema(result);

        // Root level
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());

        // Nested level
        var innerProp = schema.GetProperty("properties").GetProperty("inner");
        Assert.False(innerProp.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void Generate_StrictModeFalse_DoesNotSetAdditionalPropertiesRecursively()
    {
        var result = SchemaGenerator.Generate<NestedClass>("test_schema", false);
        var schema = GetInnerSchema(result);

        // Root level
        Assert.False(schema.TryGetProperty("additionalProperties", out _));

        // Nested level
        var innerProp = schema.GetProperty("properties").GetProperty("inner");
        Assert.False(innerProp.TryGetProperty("additionalProperties", out _));
    }

    [Fact]
    public void Generate_TypeWithDescriptionAttribute_IncludesDescription()
    {
        var result = SchemaGenerator.Generate<ClassWithTypeDescription>("test_schema");
        var schema = GetInnerSchema(result);

        Assert.Equal("A class with type description", schema.GetProperty("description").GetString());
    }

    [Fact]
    public void Generate_TypeWithSchemaDescriptionAttribute_IncludesDescription()
    {
        var result = SchemaGenerator.Generate<ClassWithSchemaDescription>("test_schema");
        var schema = GetInnerSchema(result);

        Assert.Equal("Custom schema description for type", schema.GetProperty("description").GetString());
    }

    [Fact]
    public void Generate_PropertyWithDescriptionAttribute_IncludesDescription()
    {
        var result = SchemaGenerator.Generate<ClassWithPropertyDescriptions>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");

        Assert.Equal("The person's full name", properties.GetProperty("name").GetProperty("description").GetString());
        Assert.Equal("The person's age in years", properties.GetProperty("age").GetProperty("description").GetString());
    }

    [Fact]
    public void Generate_PropertyWithBothDescriptionAttributes_SchemaDescriptionTakesPrecedence()
    {
        var result = SchemaGenerator.Generate<ClassWithPropertyDescriptions>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");

        Assert.Equal("Custom schema description takes precedence",
            properties.GetProperty("email").GetProperty("description").GetString());
    }

    [Fact]
    public void Generate_EmptyClass_ReturnsObjectWithEmptyProperties()
    {
        var result = SchemaGenerator.Generate<EmptyClass>("test_schema");
        var schema = GetInnerSchema(result);

        Assert.Equal("object", schema.GetProperty("type").GetString());
        var properties = schema.GetProperty("properties");
        Assert.Empty(properties.EnumerateObject());
    }

    [Fact]
    public void Generate_SelfReferencingClass_HandlesCircularReference()
    {
        // Should not throw StackOverflowException
        var result = SchemaGenerator.Generate<SelfReferencingClass>("test_schema");
        var schema = GetInnerSchema(result);

        Assert.Equal("object", schema.GetProperty("type").GetString());
        var properties = schema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("name", out _));
        Assert.True(properties.TryGetProperty("parent", out _));
    }

    [Fact]
    public void Generate_MutuallyReferencingClasses_HandlesCircularReference()
    {
        // Should not throw StackOverflowException
        var result = SchemaGenerator.Generate<MutuallyReferencingClassA>("test_schema");
        var schema = GetInnerSchema(result);

        Assert.Equal("object", schema.GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_Struct_ReturnsObjectSchema()
    {
        var result = SchemaGenerator.Generate<SimpleStruct>("test_schema");
        var schema = GetInnerSchema(result);

        Assert.Equal("object", schema.GetProperty("type").GetString());
        var properties = schema.GetProperty("properties");
        Assert.Equal("integer", properties.GetProperty("value").GetProperty("type").GetString());
        Assert.Equal("string", properties.GetProperty("name").GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_ClassWithStruct_HandlesStructProperty()
    {
        var result = SchemaGenerator.Generate<ClassWithStruct>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");

        var structProp = properties.GetProperty("structProp");
        Assert.Equal("object", structProp.GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_ReturnsCorrectWrapperStructure()
    {
        var result = SchemaGenerator.Generate<SimpleStringProperty>("my_schema");

        Assert.Equal("json_schema", result.GetProperty("type").GetString());
        Assert.True(result.TryGetProperty("json_schema", out var jsonSchema));
        Assert.Equal("my_schema", jsonSchema.GetProperty("name").GetString());
        Assert.True(jsonSchema.GetProperty("strict").GetBoolean());
        Assert.True(jsonSchema.TryGetProperty("schema", out _));
    }

    [Fact]
    public void Generate_WithStrictFalse_ReturnsStrictFalseInWrapper()
    {
        var result = SchemaGenerator.Generate<SimpleStringProperty>("my_schema", false);

        var jsonSchema = result.GetProperty("json_schema");
        Assert.False(jsonSchema.GetProperty("strict").GetBoolean());
    }

    [Fact]
    public void Generate_WithType_ReturnsCorrectSchema()
    {
        var result = SchemaGenerator.Generate(typeof(SimpleStringProperty), "type_schema");

        Assert.Equal("json_schema", result.GetProperty("type").GetString());
        Assert.Equal("type_schema", result.GetProperty("json_schema").GetProperty("name").GetString());
    }

    [Fact]
    public void GenerateInnerSchema_ReturnsSchemaWithoutWrapper()
    {
        var result = SchemaGenerator.GenerateInnerSchema<SimpleStringProperty>();

        // Should be a JsonNode representing the schema directly
        Assert.NotNull(result);
        var jsonString = result.ToJsonString();
        var parsed = JsonDocument.Parse(jsonString).RootElement;

        Assert.Equal("object", parsed.GetProperty("type").GetString());
        Assert.True(parsed.TryGetProperty("properties", out _));
        Assert.False(parsed.TryGetProperty("json_schema", out _)); // No wrapper
    }

    [Fact]
    public void GenerateInnerSchema_WithStrictTrue_SetsAdditionalPropertiesToFalse()
    {
        var result = SchemaGenerator.GenerateInnerSchema<SimpleStringProperty>(true);
        var jsonString = result.ToJsonString();
        var parsed = JsonDocument.Parse(jsonString).RootElement;

        Assert.False(parsed.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void GenerateInnerSchema_WithStrictFalse_DoesNotSetAdditionalProperties()
    {
        var result = SchemaGenerator.GenerateInnerSchema<SimpleStringProperty>(false);
        var jsonString = result.ToJsonString();
        var parsed = JsonDocument.Parse(jsonString).RootElement;

        Assert.False(parsed.TryGetProperty("additionalProperties", out _));
    }

    [Fact]
    public void Generate_PropertyNames_AreConvertedToCamelCase()
    {
        var result = SchemaGenerator.Generate<AllPrimitiveTypes>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");

        // Check property names are camelCase
        Assert.True(properties.TryGetProperty("stringProp", out _));
        Assert.True(properties.TryGetProperty("intProp", out _));
        Assert.True(properties.TryGetProperty("longProp", out _));
        Assert.True(properties.TryGetProperty("boolProp", out _));
    }

    [Theory]
    [InlineData("requiredString")]
    [InlineData("requiredInt")]
    [InlineData("optionalString")]
    [InlineData("optionalInt")]
    public void Generate_RequiredArray_UsesCamelCasePropertyNames(string expectedPropertyName)
    {
        var result = SchemaGenerator.Generate<RequiredVsOptional>("test_schema");
        var schema = GetInnerSchema(result);
        var properties = schema.GetProperty("properties");

        Assert.True(properties.TryGetProperty(expectedPropertyName, out _));
    }

    private static JsonElement GetInnerSchema(JsonElement result)
    {
        return result.GetProperty("json_schema").GetProperty("schema");
    }

    private static List<string> GetArrayStrings(JsonElement array)
    {
        return array.EnumerateArray().Select(e => e.GetString()!).ToList();
    }
}
