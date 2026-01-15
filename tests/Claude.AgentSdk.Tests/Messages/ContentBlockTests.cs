using System.Text.Json;
using Claude.AgentSdk.Messages;

namespace Claude.AgentSdk.Tests.Messages;

/// <summary>
///     Unit tests for ContentBlock types and their JSON serialization/deserialization.
/// </summary>
public class ContentBlockTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    #region TextBlock Tests

    [Fact]
    public void TextBlock_Deserialize_BasicText()
    {
        // Arrange
        const string json = """{"type":"text","text":"Hello, world!"}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var textBlock = Assert.IsType<TextBlock>(block);
        Assert.Equal("Hello, world!", textBlock.Text);
    }

    [Fact]
    public void TextBlock_Deserialize_EmptyText()
    {
        // Arrange
        const string json = """{"type":"text","text":""}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var textBlock = Assert.IsType<TextBlock>(block);
        Assert.Equal("", textBlock.Text);
    }

    [Theory]
    [InlineData("Hello\nWorld", "text with newline")]
    [InlineData("Hello\tWorld", "text with tab")]
    [InlineData("Hello\"World", "text with quote")]
    [InlineData("Hello\\World", "text with backslash")]
    [InlineData("Hello/World", "text with forward slash")]
    [InlineData("\u0000\u0001\u0002", "text with control characters")]
    public void TextBlock_Deserialize_SpecialCharacters(string expectedText, string description)
    {
        // Arrange - testing: {description}
        _ = description; // Used for test documentation in InlineData
        var escapedText = JsonSerializer.Serialize(expectedText);
        var json = $$$"""{"type":"text","text":{{{escapedText}}}}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var textBlock = Assert.IsType<TextBlock>(block);
        Assert.Equal(expectedText, textBlock.Text);
    }

    [Fact]
    public void TextBlock_Deserialize_UnicodeText()
    {
        // Arrange
        const string json = """{"type":"text","text":"Hello \u4e16\u754c \ud83c\udf0d"}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var textBlock = Assert.IsType<TextBlock>(block);
        Assert.Contains("\u4e16\u754c", textBlock.Text); // Chinese characters
    }

    [Fact]
    public void TextBlock_Deserialize_LongText()
    {
        // Arrange
        var longText = new string('x', 100000);
        var json = $$$"""{"type":"text","text":"{{{longText}}}"}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var textBlock = Assert.IsType<TextBlock>(block);
        Assert.Equal(longText, textBlock.Text);
    }

    [Fact]
    public void TextBlock_Serialize_Roundtrip()
    {
        // Arrange
        var original = new TextBlock { Text = "Test content" };

        // Act
        var json = JsonSerializer.Serialize<ContentBlock>(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var textBlock = Assert.IsType<TextBlock>(deserialized);
        Assert.Equal(original.Text, textBlock.Text);
    }

    #endregion

    #region ThinkingBlock Tests

    [Fact]
    public void ThinkingBlock_Deserialize_BasicThinking()
    {
        // Arrange
        const string json = """{"type":"thinking","thinking":"Let me think about this...","signature":"sig123"}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var thinkingBlock = Assert.IsType<ThinkingBlock>(block);
        Assert.Equal("Let me think about this...", thinkingBlock.Thinking);
        Assert.Equal("sig123", thinkingBlock.Signature);
    }

    [Fact]
    public void ThinkingBlock_Deserialize_EmptyThinking()
    {
        // Arrange
        const string json = """{"type":"thinking","thinking":"","signature":""}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var thinkingBlock = Assert.IsType<ThinkingBlock>(block);
        Assert.Equal("", thinkingBlock.Thinking);
        Assert.Equal("", thinkingBlock.Signature);
    }

    [Fact]
    public void ThinkingBlock_Deserialize_LongThinking()
    {
        // Arrange
        var longThinking = string.Join("\n", Enumerable.Repeat("Step by step reasoning...", 1000));
        var json = new JsonObject
        {
            ["type"] = "thinking",
            ["thinking"] = longThinking,
            ["signature"] = "sig_long"
        }.ToJsonString();

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var thinkingBlock = Assert.IsType<ThinkingBlock>(block);
        Assert.Equal(longThinking, thinkingBlock.Thinking);
    }

    [Fact]
    public void ThinkingBlock_Serialize_Roundtrip()
    {
        // Arrange
        var original = new ThinkingBlock
        {
            Thinking = "My reasoning process...",
            Signature = "signature_abc123"
        };

        // Act
        var json = JsonSerializer.Serialize<ContentBlock>(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var thinkingBlock = Assert.IsType<ThinkingBlock>(deserialized);
        Assert.Equal(original.Thinking, thinkingBlock.Thinking);
        Assert.Equal(original.Signature, thinkingBlock.Signature);
    }

    [Theory]
    [InlineData("Thinking with \"quotes\"")]
    [InlineData("Thinking with\nnewlines")]
    [InlineData("Thinking with\ttabs")]
    public void ThinkingBlock_Deserialize_SpecialCharactersInThinking(string thinking)
    {
        // Arrange
        var json = new JsonObject
        {
            ["type"] = "thinking",
            ["thinking"] = thinking,
            ["signature"] = "sig"
        }.ToJsonString();

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var thinkingBlock = Assert.IsType<ThinkingBlock>(block);
        Assert.Equal(thinking, thinkingBlock.Thinking);
    }

    #endregion

    #region ToolUseBlock Tests

    [Fact]
    public void ToolUseBlock_Deserialize_BasicToolUse()
    {
        // Arrange
        const string json = """
            {
                "type": "tool_use",
                "id": "tool_123",
                "name": "calculator",
                "input": {"operation": "add", "a": 1, "b": 2}
            }
            """;

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolUseBlock = Assert.IsType<ToolUseBlock>(block);
        Assert.Equal("tool_123", toolUseBlock.Id);
        Assert.Equal("calculator", toolUseBlock.Name);
        Assert.Equal(JsonValueKind.Object, toolUseBlock.Input.ValueKind);
        Assert.Equal("add", toolUseBlock.Input.GetProperty("operation").GetString());
        Assert.Equal(1, toolUseBlock.Input.GetProperty("a").GetInt32());
        Assert.Equal(2, toolUseBlock.Input.GetProperty("b").GetInt32());
    }

    [Fact]
    public void ToolUseBlock_Deserialize_EmptyInput()
    {
        // Arrange
        const string json = """{"type":"tool_use","id":"tool_456","name":"no_args","input":{}}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolUseBlock = Assert.IsType<ToolUseBlock>(block);
        Assert.Equal("tool_456", toolUseBlock.Id);
        Assert.Equal("no_args", toolUseBlock.Name);
        Assert.Equal(JsonValueKind.Object, toolUseBlock.Input.ValueKind);
        Assert.Empty(toolUseBlock.Input.EnumerateObject());
    }

    [Fact]
    public void ToolUseBlock_Deserialize_ComplexNestedInput()
    {
        // Arrange
        const string json = """
            {
                "type": "tool_use",
                "id": "tool_complex",
                "name": "complex_tool",
                "input": {
                    "nested": {
                        "deep": {
                            "value": 42
                        }
                    },
                    "array": [1, 2, 3],
                    "mixed": [{"a": 1}, {"b": 2}]
                }
            }
            """;

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolUseBlock = Assert.IsType<ToolUseBlock>(block);
        Assert.Equal(42, toolUseBlock.Input.GetProperty("nested").GetProperty("deep").GetProperty("value").GetInt32());

        var array = toolUseBlock.Input.GetProperty("array");
        Assert.Equal(JsonValueKind.Array, array.ValueKind);
        Assert.Equal(3, array.GetArrayLength());

        var mixed = toolUseBlock.Input.GetProperty("mixed");
        Assert.Equal(2, mixed.GetArrayLength());
    }

    [Fact]
    public void ToolUseBlock_Deserialize_InputWithAllJsonTypes()
    {
        // Arrange
        const string json = """
            {
                "type": "tool_use",
                "id": "tool_types",
                "name": "all_types",
                "input": {
                    "string": "hello",
                    "number_int": 42,
                    "number_float": 3.14,
                    "boolean_true": true,
                    "boolean_false": false,
                    "null_value": null,
                    "array": [],
                    "object": {}
                }
            }
            """;

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolUseBlock = Assert.IsType<ToolUseBlock>(block);
        var input = toolUseBlock.Input;

        Assert.Equal("hello", input.GetProperty("string").GetString());
        Assert.Equal(42, input.GetProperty("number_int").GetInt32());
        Assert.Equal(3.14, input.GetProperty("number_float").GetDouble(), 2);
        Assert.True(input.GetProperty("boolean_true").GetBoolean());
        Assert.False(input.GetProperty("boolean_false").GetBoolean());
        Assert.Equal(JsonValueKind.Null, input.GetProperty("null_value").ValueKind);
        Assert.Equal(JsonValueKind.Array, input.GetProperty("array").ValueKind);
        Assert.Equal(JsonValueKind.Object, input.GetProperty("object").ValueKind);
    }

    [Theory]
    [InlineData("toolu_abc123")]
    [InlineData("toolu_01234567890abcdef")]
    [InlineData("tool_use_long_identifier_value")]
    public void ToolUseBlock_Deserialize_VariousToolIds(string toolId)
    {
        // Arrange
        var json = $$$"""{"type":"tool_use","id":"{{{toolId}}}","name":"test","input":{}}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolUseBlock = Assert.IsType<ToolUseBlock>(block);
        Assert.Equal(toolId, toolUseBlock.Id);
    }

    [Fact]
    public void ToolUseBlock_Serialize_Roundtrip()
    {
        // Arrange
        var inputJson = JsonDocument.Parse("""{"key":"value","num":123}""").RootElement;
        var original = new ToolUseBlock
        {
            Id = "tool_round",
            Name = "roundtrip_tool",
            Input = inputJson
        };

        // Act
        var json = JsonSerializer.Serialize<ContentBlock>(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolUseBlock = Assert.IsType<ToolUseBlock>(deserialized);
        Assert.Equal(original.Id, toolUseBlock.Id);
        Assert.Equal(original.Name, toolUseBlock.Name);
        Assert.Equal("value", toolUseBlock.Input.GetProperty("key").GetString());
        Assert.Equal(123, toolUseBlock.Input.GetProperty("num").GetInt32());
    }

    [Theory]
    [InlineData("read_file")]
    [InlineData("write_file")]
    [InlineData("bash")]
    [InlineData("computer")]
    [InlineData("mcp__server__tool")]
    [InlineData("Tool_With_Underscores_And_Numbers_123")]
    public void ToolUseBlock_Deserialize_VariousToolNames(string toolName)
    {
        // Arrange
        var json = $$$"""{"type":"tool_use","id":"t1","name":"{{{toolName}}}","input":{}}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolUseBlock = Assert.IsType<ToolUseBlock>(block);
        Assert.Equal(toolName, toolUseBlock.Name);
    }

    #endregion

    #region ToolResultBlock Tests

    [Fact]
    public void ToolResultBlock_Deserialize_BasicResult()
    {
        // Arrange
        const string json = """{"type":"tool_result","tool_use_id":"tool_123","content":"Operation successful"}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolResultBlock = Assert.IsType<ToolResultBlock>(block);
        Assert.Equal("tool_123", toolResultBlock.ToolUseId);
        Assert.NotNull(toolResultBlock.Content);
        Assert.Equal("Operation successful", toolResultBlock.Content.Value.GetString());
        Assert.Null(toolResultBlock.IsError);
    }

    [Fact]
    public void ToolResultBlock_Deserialize_WithIsErrorTrue()
    {
        // Arrange
        const string json = """{"type":"tool_result","tool_use_id":"tool_err","content":"Error: File not found","is_error":true}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolResultBlock = Assert.IsType<ToolResultBlock>(block);
        Assert.Equal("tool_err", toolResultBlock.ToolUseId);
        Assert.True(toolResultBlock.IsError);
        Assert.Equal("Error: File not found", toolResultBlock.Content?.GetString());
    }

    [Fact]
    public void ToolResultBlock_Deserialize_WithIsErrorFalse()
    {
        // Arrange
        const string json = """{"type":"tool_result","tool_use_id":"tool_ok","content":"Success","is_error":false}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolResultBlock = Assert.IsType<ToolResultBlock>(block);
        Assert.False(toolResultBlock.IsError);
    }

    [Fact]
    public void ToolResultBlock_Deserialize_NullContent()
    {
        // Arrange
        const string json = """{"type":"tool_result","tool_use_id":"tool_null","content":null}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolResultBlock = Assert.IsType<ToolResultBlock>(block);
        // When JSON explicitly contains "content": null, the JsonElement? is null
        Assert.Null(toolResultBlock.Content);
    }

    [Fact]
    public void ToolResultBlock_Deserialize_MissingContent()
    {
        // Arrange
        const string json = """{"type":"tool_result","tool_use_id":"tool_no_content"}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolResultBlock = Assert.IsType<ToolResultBlock>(block);
        Assert.Null(toolResultBlock.Content);
    }

    [Fact]
    public void ToolResultBlock_Deserialize_ObjectContent()
    {
        // Arrange
        const string json = """{"type":"tool_result","tool_use_id":"tool_obj","content":{"result":"data","count":5}}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolResultBlock = Assert.IsType<ToolResultBlock>(block);
        Assert.NotNull(toolResultBlock.Content);
        Assert.Equal(JsonValueKind.Object, toolResultBlock.Content.Value.ValueKind);
        Assert.Equal("data", toolResultBlock.Content.Value.GetProperty("result").GetString());
        Assert.Equal(5, toolResultBlock.Content.Value.GetProperty("count").GetInt32());
    }

    [Fact]
    public void ToolResultBlock_Deserialize_ArrayContent()
    {
        // Arrange
        const string json = """{"type":"tool_result","tool_use_id":"tool_arr","content":[1,2,3,"four",{"five":5}]}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolResultBlock = Assert.IsType<ToolResultBlock>(block);
        Assert.NotNull(toolResultBlock.Content);
        Assert.Equal(JsonValueKind.Array, toolResultBlock.Content.Value.ValueKind);
        Assert.Equal(5, toolResultBlock.Content.Value.GetArrayLength());
    }

    [Fact]
    public void ToolResultBlock_Serialize_Roundtrip()
    {
        // Arrange
        var contentJson = JsonDocument.Parse("""{"output":"test result"}""").RootElement;
        var original = new ToolResultBlock
        {
            ToolUseId = "tool_rt",
            Content = contentJson,
            IsError = false
        };

        // Act
        var json = JsonSerializer.Serialize<ContentBlock>(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolResultBlock = Assert.IsType<ToolResultBlock>(deserialized);
        Assert.Equal(original.ToolUseId, toolResultBlock.ToolUseId);
        Assert.Equal(original.IsError, toolResultBlock.IsError);
        Assert.Equal("test result", toolResultBlock.Content?.GetProperty("output").GetString());
    }

    [Fact]
    public void ToolResultBlock_Deserialize_LargeContent()
    {
        // Arrange
        var largeArray = Enumerable.Range(0, 10000).ToArray();
        var largeContent = JsonSerializer.Serialize(largeArray);
        var json = $$$"""{"type":"tool_result","tool_use_id":"tool_large","content":{{{largeContent}}}}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var toolResultBlock = Assert.IsType<ToolResultBlock>(block);
        Assert.Equal(10000, toolResultBlock.Content?.GetArrayLength());
    }

    #endregion

    #region Polymorphic Type Discrimination Tests

    [Theory]
    [InlineData("""{"type":"text","text":"hello"}""", typeof(TextBlock))]
    [InlineData("""{"type":"thinking","thinking":"hmm","signature":"sig"}""", typeof(ThinkingBlock))]
    [InlineData("""{"type":"tool_use","id":"t","name":"n","input":{}}""", typeof(ToolUseBlock))]
    [InlineData("""{"type":"tool_result","tool_use_id":"t"}""", typeof(ToolResultBlock))]
    public void ContentBlock_Deserialize_CorrectTypeDiscrimination(string json, Type expectedType)
    {
        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        Assert.NotNull(block);
        Assert.IsType(expectedType, block);
    }

    [Fact]
    public void ContentBlock_Deserialize_UnknownType_ThrowsException()
    {
        // Arrange
        const string json = """{"type":"unknown_block_type","data":"test"}""";

        // Act & Assert - System.Text.Json throws NotSupportedException wrapped in JsonException for unknown discriminators
        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions));
    }

    [Fact]
    public void ContentBlock_Deserialize_MissingType_ThrowsException()
    {
        // Arrange
        const string json = """{"text":"hello"}""";

        // Act & Assert - System.Text.Json throws NotSupportedException when type discriminator is missing
        Assert.ThrowsAny<NotSupportedException>(() => JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions));
    }

    [Fact]
    public void ContentBlock_Serialize_IncludesTypeDiscriminator()
    {
        // Arrange
        var blocks = new ContentBlock[]
        {
            new TextBlock { Text = "test" },
            new ThinkingBlock { Thinking = "think", Signature = "sig" },
            new ToolUseBlock { Id = "t", Name = "n", Input = JsonDocument.Parse("{}").RootElement },
            new ToolResultBlock { ToolUseId = "t" }
        };

        // Act & Assert
        foreach (var block in blocks)
        {
            var json = JsonSerializer.Serialize(block, SerializerOptions);
            Assert.Contains("\"type\":", json);
        }
    }

    #endregion

    #region Array/Collection Tests

    [Fact]
    public void ContentBlockArray_Deserialize_MixedTypes()
    {
        // Arrange
        const string json = """
            [
                {"type":"text","text":"Hello"},
                {"type":"thinking","thinking":"Let me think","signature":"sig"},
                {"type":"tool_use","id":"t1","name":"bash","input":{"command":"ls"}},
                {"type":"tool_result","tool_use_id":"t1","content":"file1.txt\nfile2.txt"}
            ]
            """;

        // Act
        var blocks = JsonSerializer.Deserialize<ContentBlock[]>(json, SerializerOptions);

        // Assert
        Assert.NotNull(blocks);
        Assert.Equal(4, blocks.Length);
        Assert.IsType<TextBlock>(blocks[0]);
        Assert.IsType<ThinkingBlock>(blocks[1]);
        Assert.IsType<ToolUseBlock>(blocks[2]);
        Assert.IsType<ToolResultBlock>(blocks[3]);
    }

    [Fact]
    public void ContentBlockArray_Deserialize_EmptyArray()
    {
        // Arrange
        const string json = "[]";

        // Act
        var blocks = JsonSerializer.Deserialize<ContentBlock[]>(json, SerializerOptions);

        // Assert
        Assert.NotNull(blocks);
        Assert.Empty(blocks);
    }

    [Fact]
    public void ContentBlockArray_Deserialize_SingleElement()
    {
        // Arrange
        const string json = """[{"type":"text","text":"single"}]""";

        // Act
        var blocks = JsonSerializer.Deserialize<ContentBlock[]>(json, SerializerOptions);

        // Assert
        Assert.NotNull(blocks);
        Assert.Single(blocks);
        var textBlock = Assert.IsType<TextBlock>(blocks[0]);
        Assert.Equal("single", textBlock.Text);
    }

    [Fact]
    public void ContentBlockArray_Serialize_Roundtrip()
    {
        // Arrange
        var original = new ContentBlock[]
        {
            new TextBlock { Text = "First" },
            new TextBlock { Text = "Second" },
            new ToolUseBlock { Id = "t", Name = "test", Input = JsonDocument.Parse("{}").RootElement }
        };

        // Act
        var json = JsonSerializer.Serialize(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<ContentBlock[]>(json, SerializerOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Length, deserialized.Length);
        Assert.Equal("First", ((TextBlock)deserialized[0]).Text);
        Assert.Equal("Second", ((TextBlock)deserialized[1]).Text);
        Assert.Equal("test", ((ToolUseBlock)deserialized[2]).Name);
    }

    [Fact]
    public void ContentBlockList_Deserialize_Works()
    {
        // Arrange
        const string json = """[{"type":"text","text":"list item"}]""";

        // Act
        var blocks = JsonSerializer.Deserialize<List<ContentBlock>>(json, SerializerOptions);

        // Assert
        Assert.NotNull(blocks);
        Assert.Single(blocks);
    }

    [Fact]
    public void ContentBlockArray_Deserialize_MultipleToolUsesAndResults()
    {
        // Arrange - simulates a conversation with multiple tool calls
        const string json = """
            [
                {"type":"text","text":"I'll help you with that."},
                {"type":"tool_use","id":"t1","name":"read_file","input":{"path":"/test.txt"}},
                {"type":"tool_use","id":"t2","name":"bash","input":{"command":"pwd"}},
                {"type":"tool_result","tool_use_id":"t1","content":"file contents here"},
                {"type":"tool_result","tool_use_id":"t2","content":"/home/user"},
                {"type":"text","text":"Based on the results..."}
            ]
            """;

        // Act
        var blocks = JsonSerializer.Deserialize<ContentBlock[]>(json, SerializerOptions);

        // Assert
        Assert.NotNull(blocks);
        Assert.Equal(6, blocks.Length);

        var toolUses = blocks.OfType<ToolUseBlock>().ToList();
        Assert.Equal(2, toolUses.Count);
        Assert.Equal("read_file", toolUses[0].Name);
        Assert.Equal("bash", toolUses[1].Name);

        var toolResults = blocks.OfType<ToolResultBlock>().ToList();
        Assert.Equal(2, toolResults.Count);
        Assert.Equal("t1", toolResults[0].ToolUseId);
        Assert.Equal("t2", toolResults[1].ToolUseId);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void ContentBlock_Deserialize_ExtraProperties_Ignored()
    {
        // Arrange - JSON with extra unknown properties
        const string json = """{"type":"text","text":"hello","extra_field":"ignored","another":123}""";

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var textBlock = Assert.IsType<TextBlock>(block);
        Assert.Equal("hello", textBlock.Text);
    }

    [Fact]
    public void ContentBlock_Deserialize_WhitespaceInJson()
    {
        // Arrange
        const string json = """
            {
                "type"   :    "text"   ,
                "text"   :    "spaced out"
            }
            """;

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var textBlock = Assert.IsType<TextBlock>(block);
        Assert.Equal("spaced out", textBlock.Text);
    }

    [Fact]
    public void TextBlock_Deserialize_MissingRequiredText_ThrowsException()
    {
        // Arrange
        const string json = """{"type":"text"}""";

        // Act & Assert
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions));
    }

    [Fact]
    public void ThinkingBlock_Deserialize_MissingSignature_ThrowsException()
    {
        // Arrange
        const string json = """{"type":"thinking","thinking":"test"}""";

        // Act & Assert
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions));
    }

    [Fact]
    public void ToolUseBlock_Deserialize_MissingId_ThrowsException()
    {
        // Arrange
        const string json = """{"type":"tool_use","name":"test","input":{}}""";

        // Act & Assert
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions));
    }

    [Fact]
    public void ToolUseBlock_Deserialize_MissingName_ThrowsException()
    {
        // Arrange
        const string json = """{"type":"tool_use","id":"t1","input":{}}""";

        // Act & Assert
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions));
    }

    [Fact]
    public void ToolUseBlock_Deserialize_MissingInput_ThrowsException()
    {
        // Arrange
        const string json = """{"type":"tool_use","id":"t1","name":"test"}""";

        // Act & Assert
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions));
    }

    [Fact]
    public void ToolResultBlock_Deserialize_MissingToolUseId_ThrowsException()
    {
        // Arrange
        const string json = """{"type":"tool_result","content":"result"}""";

        // Act & Assert
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{")]
    [InlineData("[")]
    [InlineData("null")]
    public void ContentBlock_Deserialize_InvalidJson_ThrowsOrReturnsNull(string invalidJson)
    {
        // Act & Assert
        if (invalidJson == "null")
        {
            var result = JsonSerializer.Deserialize<ContentBlock>(invalidJson, SerializerOptions);
            Assert.Null(result);
        }
        else
        {
            Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<ContentBlock>(invalidJson, SerializerOptions));
        }
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void TextBlock_Equality_SameValues_AreEqual()
    {
        // Arrange
        var block1 = new TextBlock { Text = "test" };
        var block2 = new TextBlock { Text = "test" };

        // Assert
        Assert.Equal(block1, block2);
        Assert.True(block1 == block2);
    }

    [Fact]
    public void TextBlock_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var block1 = new TextBlock { Text = "test1" };
        var block2 = new TextBlock { Text = "test2" };

        // Assert
        Assert.NotEqual(block1, block2);
        Assert.True(block1 != block2);
    }

    [Fact]
    public void ThinkingBlock_Equality_Works()
    {
        // Arrange
        var block1 = new ThinkingBlock { Thinking = "think", Signature = "sig" };
        var block2 = new ThinkingBlock { Thinking = "think", Signature = "sig" };
        var block3 = new ThinkingBlock { Thinking = "think", Signature = "different" };

        // Assert
        Assert.Equal(block1, block2);
        Assert.NotEqual(block1, block3);
    }

    [Fact]
    public void ToolResultBlock_Equality_WithNullContent()
    {
        // Arrange
        var block1 = new ToolResultBlock { ToolUseId = "t1", Content = null };
        var block2 = new ToolResultBlock { ToolUseId = "t1", Content = null };

        // Assert
        Assert.Equal(block1, block2);
    }

    #endregion

    #region Real-World Scenario Tests

    [Fact]
    public void ContentBlock_Deserialize_RealWorldAssistantResponse()
    {
        // Arrange - simulates a real Claude response with thinking and tool use
        const string json = """
            [
                {
                    "type": "thinking",
                    "thinking": "The user wants me to read a file. I should use the read_file tool with the provided path.",
                    "signature": "EqoBCkYIAhILY2xhdWRlLW5vdGUiL1RoaXMgaXMgYSBzaWduYXR1cmUgZm9yIHRoZSBleHRlbmRlZCB0aGlua2luZyBibG9jaw=="
                },
                {
                    "type": "text",
                    "text": "I'll read that file for you."
                },
                {
                    "type": "tool_use",
                    "id": "toolu_01ABC123XYZ",
                    "name": "read_file",
                    "input": {
                        "file_path": "/home/user/document.txt",
                        "encoding": "utf-8"
                    }
                }
            ]
            """;

        // Act
        var blocks = JsonSerializer.Deserialize<ContentBlock[]>(json, SerializerOptions);

        // Assert
        Assert.NotNull(blocks);
        Assert.Equal(3, blocks.Length);

        var thinking = Assert.IsType<ThinkingBlock>(blocks[0]);
        Assert.Contains("read_file tool", thinking.Thinking);

        var text = Assert.IsType<TextBlock>(blocks[1]);
        Assert.Equal("I'll read that file for you.", text.Text);

        var toolUse = Assert.IsType<ToolUseBlock>(blocks[2]);
        Assert.Equal("toolu_01ABC123XYZ", toolUse.Id);
        Assert.Equal("read_file", toolUse.Name);
        Assert.Equal("/home/user/document.txt", toolUse.Input.GetProperty("file_path").GetString());
    }

    [Fact]
    public void ContentBlock_Deserialize_RealWorldToolResultWithError()
    {
        // Arrange - simulates a tool result with error
        const string json = """
            {
                "type": "tool_result",
                "tool_use_id": "toolu_01XYZ789",
                "content": "Error: Permission denied. Cannot read file '/etc/shadow'. You need elevated privileges to access this file.",
                "is_error": true
            }
            """;

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var result = Assert.IsType<ToolResultBlock>(block);
        Assert.Equal("toolu_01XYZ789", result.ToolUseId);
        Assert.True(result.IsError);
        Assert.Contains("Permission denied", result.Content?.GetString());
    }

    [Fact]
    public void ContentBlock_Deserialize_BashToolOutput()
    {
        // Arrange - simulates bash tool result with multi-line output
        const string json = """
            {
                "type": "tool_result",
                "tool_use_id": "toolu_bash_001",
                "content": "total 24\ndrwxr-xr-x  5 user user 4096 Jan 10 10:00 .\ndrwxr-xr-x  3 user user 4096 Jan  9 09:00 ..\n-rw-r--r--  1 user user  512 Jan 10 10:00 file.txt\n-rw-r--r--  1 user user 1024 Jan 10 09:30 config.json",
                "is_error": false
            }
            """;

        // Act
        var block = JsonSerializer.Deserialize<ContentBlock>(json, SerializerOptions);

        // Assert
        var result = Assert.IsType<ToolResultBlock>(block);
        Assert.False(result.IsError);
        var content = result.Content?.GetString();
        Assert.NotNull(content);
        Assert.Contains("file.txt", content);
        Assert.Contains("config.json", content);
    }

    #endregion

    #region Helper Class for JSON Construction

    /// <summary>
    ///     Simple helper for building JSON objects in tests.
    /// </summary>
    private class JsonObject : Dictionary<string, object?>
    {
        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    #endregion
}
